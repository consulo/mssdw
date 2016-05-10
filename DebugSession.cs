using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Threading;
using Consulo.Internal.Mssdw.Network;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.CorSymbolStore;

namespace Consulo.Internal.Mssdw
{
	public class DebugSession
	{
		class ModuleInfo
		{
			public ISymbolReader Reader;
			public CorModule Module;
			public CorMetadataImport Importer;
			public int References;
		}

		public class DocInfo
		{
			public ISymbolReader Reader;
			public ISymbolDocument Document;
			public CorModule Module;
		}

		private readonly object terminateLock = new object();
		private readonly object debugLock = new object();
		internal readonly Dictionary<string, DocInfo> documents = new Dictionary<string, DocInfo>(StringComparer.CurrentCultureIgnoreCase);
		private readonly Dictionary<string, ModuleInfo> modules = new Dictionary<string, ModuleInfo>();
		private readonly SymbolBinder symbolBinder = new SymbolBinder();
		readonly Dictionary<CorBreakpoint, EventRequest> breakpoints = new Dictionary<CorBreakpoint, EventRequest>();
		private readonly Dictionary<int, EventRequest> myEventRequests = new Dictionary<int, EventRequest>();

		private EventRequest myModuleLoadRequest;

		private CorProcess process;
		private CorDebugger dbg;
		private int processId;
		private bool evaluating;
		private CorThread activeThread;
		private CorStepper stepper;
		private bool autoStepInto;
		private bool stepInsideDebuggerHidden = false;
		private Semaphore myEvalSemaphore;

		public CorThread ActiveThread
		{
			get
			{
				return activeThread;
			}
		}

		private readonly MdwpConnection myConnection;

		internal DebugSession(MdwpConnection connection)
		{
			myConnection = connection;
		}

		public void AddEventRequest(EventRequest eventRequest)
		{
			myEventRequests.Add(eventRequest.RequestId, eventRequest);

			switch(eventRequest.EventKind)
			{
				case EventKind.BREAKPOINT:
					InsertBreakpoint(eventRequest);
					break;
				case EventKind.MODULE_LOAD:
					myModuleLoadRequest = eventRequest;
					break;
			}
		}

		public void RemoveBreakpointEventRequests()
		{
			foreach (EventRequest requestsValue in myEventRequests.Values)
			{
				CorFunctionBreakpoint functionBreakpoint = requestsValue.Data as CorFunctionBreakpoint;

				if(functionBreakpoint != null)
				{
					functionBreakpoint.Active = false;
				}
			}

			myEventRequests.Clear();
		}

		public void RemoveEventRequest(int requestId)
		{
			EventRequest eventRequest = myEventRequests[requestId];

			myEventRequests.Remove(requestId);

			switch(eventRequest.EventKind)
			{
				case EventKind.BREAKPOINT:
					CorFunctionBreakpoint functionBreakpoint = eventRequest.Data as CorFunctionBreakpoint;
					if(functionBreakpoint != null)
					{
						functionBreakpoint.Active = false;
					}
					break;
				case EventKind.MODULE_LOAD:
					myModuleLoadRequest = null;
					break;
			}
		}

		public void Start(List<string> args)
		{
			string command = args[0];
			string commandLine = string.Join(" ", args);
			DirectoryInfo parentDirectory = Directory.GetParent(command);

			if(!File.Exists(command))
			{
				throw new Exception(string.Format("File '{0}' is not exists", command));
			}
			// Create the debugger
			string dversion;
			try
			{
				dversion = CorDebugger.GetDebuggerVersionFromFile(command);
			}
			catch
			{
				dversion = CorDebugger.GetDefaultDebuggerVersion();
			}
			dbg = new CorDebugger(dversion);

			Dictionary<string, string> env = new Dictionary<string, string>();
			foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
				env[(string)de.Key] = (string)de.Value;

			process = dbg.CreateProcess(command, commandLine, parentDirectory.FullName, env, 0);
			processId = process.Id;

			process.OnCreateProcess += new CorProcessEventHandler(OnCreateProcess);
			process.OnCreateAppDomain += new CorAppDomainEventHandler(OnCreateAppDomain);
			/*  process.OnAssemblyLoad += new CorAssemblyEventHandler(OnAssemblyLoad);
			  process.OnAssemblyUnload += new CorAssemblyEventHandler(OnAssemblyUnload);
			  process.OnCreateThread += new CorThreadEventHandler(OnCreateThread);
			  process.OnThreadExit += new CorThreadEventHandler(OnThreadExit);*/
			process.OnModuleLoad += new CorModuleEventHandler(OnModuleLoad);
			process.OnModuleUnload += new CorModuleEventHandler(OnModuleUnload);
			process.OnProcessExit += new CorProcessEventHandler(OnProcessExit);
			/*  process.OnUpdateModuleSymbols += new UpdateModuleSymbolsEventHandler(OnUpdateModuleSymbols);
			  process.OnDebuggerError += new DebuggerErrorEventHandler(OnDebuggerError);*/
			process.OnBreakpoint += new BreakpointEventHandler(OnBreakpoint);
			process.OnStepComplete += new StepCompleteEventHandler(OnStepComplete);
			/*  process.OnBreak += new CorThreadEventHandler(OnBreak);
			  process.OnNameChange += new CorThreadEventHandler(OnNameChange);   */
			process.OnEvalComplete += new EvalEventHandler(OnEvalComplete);
			process.OnEvalException += new EvalEventHandler(OnEvalException);
			/*process.OnLogMessage += new LogMessageEventHandler(OnLogMessage);
			process.OnException2 += new CorException2EventHandler(OnException2);*/
			//process.RegisterStdOutput(OnStdOutput);

			SendEvent(EventKind.VM_START);
		}

		internal void OnEvalComplete(object sender, CorEvalEventArgs e)
		{
			if(myEvalSemaphore != null)
			{
				myEvalSemaphore.Release();
				e.Continue = false;
			}
		}

		internal void OnEvalException(object sender, CorEvalEventArgs e)
		{
			if(myEvalSemaphore != null)
			{
				myEvalSemaphore.Release();
				e.Continue = false;
			}
		}

		internal CorValue Evaluate(CorThread corThread, Action<CorEval> action)
		{
			lock (debugLock)
			{
				CorEval corEval = corThread.CreateEval();

				myEvalSemaphore = new Semaphore(0, 1);
				action(corEval);
				process.SetAllThreadsDebugState(CorDebugThreadState.THREAD_SUSPEND, corThread);
				ClearEvalStatus();
				OnStartEvaluating();
				Process.Continue(false);
				myEvalSemaphore.WaitOne();
				OnEndEvaluating();

				return corEval.Result;
			}
		}

		void Step(bool into)
		{
			try
			{
				if(stepper != null)
				{
					CorFrame frame = activeThread.ActiveFrame;
					ISymbolReader reader = GetReaderForModule(frame.Function.Module.Name);
					if(reader == null)
					{
						RawContinue(into);
						return;
					}
					ISymbolMethod met = reader.GetMethod(new SymbolToken(frame.Function.Token));
					if(met == null)
					{
						RawContinue(into);
						return;
					}

					uint offset;
					CorDebugMappingResult mappingResult;
					frame.GetIP(out offset, out mappingResult);

					// Exclude all ranges belonging to the current line
					List<COR_DEBUG_STEP_RANGE> ranges = new List<COR_DEBUG_STEP_RANGE>();
					var sequencePoints = met.GetSequencePoints().ToArray();
					for(int i = 0; i < sequencePoints.Length; i++)
					{
						if(sequencePoints[i].Offset > offset)
						{
							var r = new COR_DEBUG_STEP_RANGE();
							r.startOffset = i == 0 ? 0 : (uint)sequencePoints[i - 1].Offset;
							r.endOffset = (uint)sequencePoints[i].Offset;
							ranges.Add(r);
							break;
						}
					}
					if(ranges.Count == 0 && sequencePoints.Length > 0)
					{
						var r = new COR_DEBUG_STEP_RANGE();
						r.startOffset = (uint)sequencePoints[sequencePoints.Length - 1].Offset;
						r.endOffset = uint.MaxValue;
						ranges.Add(r);
					}

					stepper.StepRange(into, ranges.ToArray());

					ClearEvalStatus();
					process.SetAllThreadsDebugState(CorDebugThreadState.THREAD_RUN, null);
					process.Continue(false);
				}
			}
			catch(Exception e)
			{
				OnDebuggerOutput(true, e.ToString());
			}
		}

		void StepOut()
		{
			if(stepper != null)
			{
				stepper.StepOut();
				ClearEvalStatus();
				process.SetAllThreadsDebugState(CorDebugThreadState.THREAD_RUN, null);
				process.Continue(false);
			}
		}

		private void RawContinue(bool into, bool stepOverAll = false)
		{
			if(stepOverAll)
				stepper.StepRange(into, new[]{ new COR_DEBUG_STEP_RANGE()
				{
					startOffset = 0,
					endOffset = uint.MaxValue
				} });
			else
				stepper.Step(into);
			ClearEvalStatus();
			process.Continue(false);
		}

		public CorProcess Process
		{
			get
			{
				return process;
			}
		}

		internal static IEnumerable<CorFrame> GetFrames(CorThread thread)
		{
			foreach (CorChain chain in thread.Chains)
			{
				if(!chain.IsManaged)
					continue;
				foreach (CorFrame frame in chain.Frames)
					yield return frame;
			}
		}

		void SetActiveThread(CorThread t)
		{
			activeThread = t;
			if(stepper != null && stepper.IsActive())
			{
				stepper.Deactivate();
			}
			stepper = activeThread.CreateStepper();
			stepper.SetUnmappedStopMask(CorDebugUnmappedStop.STOP_NONE);
			stepper.SetJmcStatus(true);
		}

		public enum BreakEventStatus
		{
			NoDoc,
			Invalid,
			NoMethod,
			Bound
		}

		public void InsertBreakpoint(EventRequest request)
		{
			BreakpointLocation location = request.FindModifier<BreakpointLocation>();
			CorMetadataImport module = GetMetadataForModule(location.ModulePath);
			CorFunction func = module.Module.GetFunctionFromToken(location.MethodToken);
			CorFunctionBreakpoint corBp = func.ILCode.CreateBreakpoint(location.Offset);
			corBp.Active = true;

			breakpoints[corBp] = request;
			request.Data = corBp;
		}

		void OnBreakpoint(object sender, CorBreakpointEventArgs e)
		{
			lock (debugLock)
			{
				if(evaluating)
				{
					e.Continue = true;
					return;
				}
			}

			EventRequest eventRequest;
			if(!breakpoints.TryGetValue(e.Breakpoint, out eventRequest))
			{
				e.Continue = true;
				return;
			}

			if(e.AppDomain.Process.HasQueuedCallbacks(e.Thread))
			{
				e.Continue = true;
				return;
			}

			// If a breakpoint is hit while stepping, cancel the stepping operation
			if(stepper != null && stepper.IsActive())
			{
				stepper.Deactivate();
			}

			autoStepInto = false;

			SetActiveThread(e.Thread);

			e.Continue = false;

			ReplyEvent(eventRequest, packet =>
			{
				packet.WriteInt(e.Thread.Id);  // thread id
			});
		}

		void OnStepComplete(object sender, CorStepCompleteEventArgs e)
		{
			lock (debugLock)
			{
				if(evaluating)
				{
					e.Continue = true;
					return;
				}
			}

			bool localAutoStepInto = autoStepInto;
			autoStepInto = false;
			bool localStepInsideDebuggerHidden = stepInsideDebuggerHidden;
			stepInsideDebuggerHidden = false;

			if(e.AppDomain.Process.HasQueuedCallbacks(e.Thread))
			{
				e.Continue = true;
				return;
			}

			if(localAutoStepInto)
			{
				Step(true);
				e.Continue = true;
				return;
			}

			if(ContinueOnStepIn(e.Thread.ActiveFrame.Function.GetMethodInfo(this)))
			{
				e.Continue = true;
				return;
			}

			var currentSequence = Consulo.Internal.Mssdw.Network.Handle.ThreadHandle.GetSequencePoint(this, e.Thread.ActiveFrame);
			if(currentSequence == null)
			{
				stepper.StepOut();
				autoStepInto = true;
				e.Continue = true;
				return;
			}

			if(StepThrough(e.Thread.ActiveFrame.Function.GetMethodInfo(this)))
			{
				stepInsideDebuggerHidden = e.StepReason == CorDebugStepReason.STEP_CALL;
				RawContinue(true, true);
				e.Continue = true;
				return;
			}

			if(currentSequence.IsSpecial)
			{
				Step(false);
				e.Continue = true;
				return;
			}

			if(localStepInsideDebuggerHidden && e.StepReason == CorDebugStepReason.STEP_RETURN)
			{
				Step(true);
				e.Continue = true;
				return;
			}

			e.Continue = false;
			SetActiveThread(e.Thread);
		}

		bool StepThrough(MetadataMethodInfo methodInfo)
		{
			return methodInfo.GetCustomAttributes(true).Union(methodInfo.DeclaringType.GetCustomAttributes(true)).Any(v =>
			v is System.Diagnostics.DebuggerHiddenAttribute ||
			v is System.Diagnostics.DebuggerStepThroughAttribute);
		}

		bool ContinueOnStepIn(MetadataMethodInfo methodInfo)
		{
			return methodInfo.GetCustomAttributes(true).Any(v => v is System.Diagnostics.DebuggerStepperBoundaryAttribute);
		}

		void OnModuleLoad(object sender, CorModuleEventArgs e)
		{
			CorMetadataImport mi = new CorMetadataImport(this, e.Module);

			try
			{
				// Required to avoid the jit to get rid of variables too early
				e.Module.JITCompilerFlags = CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION;
			}
			catch
			{
				// Some kind of modules don't allow JIT flags to be changed.
			}

			string file = e.Module.Assembly.Name;
			lock (documents)
			{
				ISymbolReader reader = null;
				if(System.IO.File.Exists(System.IO.Path.ChangeExtension(file, ".pdb")))
				{
					try
					{
						reader = symbolBinder.GetReaderForFile(mi.RawCOMObject, file, ".");
						foreach (ISymbolDocument doc in reader.GetDocuments())
						{
							if(string.IsNullOrEmpty(doc.URL))
								continue;
							string docFile = System.IO.Path.GetFullPath(doc.URL);
							DocInfo di = new DocInfo();
							di.Document = doc;
							di.Reader = reader;
							di.Module = e.Module;
							documents[docFile] = di;
						}
					}
					catch(Exception ex)
					{
						OnDebuggerOutput(true, string.Format("Debugger Error: {0}\n", ex.Message));
					}
					e.Module.SetJmcStatus(true, null);
				}
				else
				{
					// Flag modules without debug info as not JMC. In this way
					// the debugger won't try to step into them
					e.Module.SetJmcStatus(false, null);
				}

				ModuleInfo moi;

				if(modules.TryGetValue(e.Module.Name, out moi))
				{
					moi.References++;
				}
				else
				{
					moi = new ModuleInfo();
					moi.Module = e.Module;
					moi.Reader = reader;
					moi.Importer = mi;
					moi.References = 1;
					modules[e.Module.Name] = moi;
				}
			}

			if(myModuleLoadRequest != null)
			{
				e.Continue = false;
				ReplyEvent(myModuleLoadRequest, packet =>
				{
					packet.WriteString(file);  // file
				});
			}
			else
			{
				e.Continue = true;
			}
		}

		private void OnModuleUnload(object sender, CorModuleEventArgs e)
		{
			lock (documents)
			{
				ModuleInfo moi;
				modules.TryGetValue(e.Module.Name, out moi);
				if(moi == null || --moi.References > 0)
					return;

				modules.Remove(e.Module.Name);
				List<string> toRemove = new List<string>();
				foreach (KeyValuePair<string, DocInfo> di in documents)
				{
					if(di.Value.Module.Name == e.Module.Name)
						toRemove.Add(di.Key);
				}
				foreach (string file in toRemove)
				{
					documents.Remove(file);
				}
			}
		}

		public void OnCreateAppDomain(object sender, CorAppDomainEventArgs e)
		{
			e.AppDomain.Attach();
			e.Continue = true;
		}

		public void OnCreateProcess(object sender, CorProcessEventArgs e)
		{
			// Required to avoid the jit to get rid of variables too early
			e.Process.DesiredNGENCompilerFlags = CorDebugJITCompilerFlags.CORDEBUG_JIT_DISABLE_OPTIMIZATION;
			e.Process.EnableLogMessages(true);
			e.Continue = true;
		}

		public void OnProcessExit(object sender, CorProcessEventArgs e)
		{
			// If the main thread stopped, terminate the debugger session
			if(e.Process.Id == process.Id)
			{
				lock (terminateLock)
				{
					process.Dispose();
					process = null;
					ThreadPool.QueueUserWorkItem(delegate
					{
						// The Terminate call will fail if called in the event handler
						dbg.Terminate();
						dbg = null;
					});

					SendEvent(EventKind.VM_DEATH);

					myConnection.Close();
				}
			}
		}

		private void OnDebuggerOutput(bool error, string message)
		{
			if(error)
			{
				Console.Error.WriteLine(message);
			}
			else
			{
				Console.WriteLine(message);
			}
		}

		internal CorMetadataImport GetMSCorLibModule()
		{
			lock (documents)
			{
				CorModule corModule = null;
				foreach (ModuleInfo value in modules.Values)
				{
					CorModule module = value.Module;
					if("mscorlib.dll".Equals(Path.GetFileName(module.Name), StringComparison.InvariantCultureIgnoreCase))
					{
						corModule = module;
					}
				}
				if(corModule == null)
				{
					return null;
				}

				ModuleInfo mod;
				if(!modules.TryGetValue(System.IO.Path.GetFullPath(corModule.Name), out mod))
					return null;
				return mod.Importer;
			}
		}

		internal CorModule GetCorModuleForToken(int token)
		{
			lock (documents)
			{
				ModuleInfo moduleInfo = null;
				foreach (ModuleInfo value in modules.Values)
				{
					CorModule module = value.Module;
					if(module.Token == token)
					{
						moduleInfo = value;
					}
				}
				return moduleInfo == null ? null : moduleInfo.Module;
			}
		}

		internal CorMetadataImport GetMetadataForModule(string file)
		{
			lock (documents)
			{
				ModuleInfo mod;
				if(!modules.TryGetValue(System.IO.Path.GetFullPath(file), out mod))
					return null;
				return mod.Importer;
			}
		}

		internal ISymbolReader GetReaderForModule(string file)
		{
			lock (documents)
			{
				ModuleInfo mod;
				if(!modules.TryGetValue(System.IO.Path.GetFullPath(file), out mod))
					return null;
				return mod.Reader;
			}
		}

		internal TypeRef FindTypeByName(string name)
		{
			lock (documents)
			{
				foreach (ModuleInfo value in modules.Values)
				{
					int tokenFromName = value.Importer.GetTypeTokenFromName(name);
					if(tokenFromName > 0)
					{
						return new TypeRef(value.Module.Name, tokenFromName);
					}
				}
				return null;
			}
		}

		void OnStartEvaluating()
		{
			lock (debugLock)
			{
				evaluating = true;
			}
		}

		void OnEndEvaluating()
		{
			lock (debugLock)
			{
				evaluating = false;
				Monitor.PulseAll(debugLock);
			}
		}

		public void WaitUntilStopped()
		{
			lock (debugLock)
			{
				while(evaluating)
					Monitor.Wait(debugLock);
			}
		}

		public CorThread GetThread(int id)
		{
			try
			{
				WaitUntilStopped();
				foreach (CorThread t in process.Threads)
					if(t.Id == id)
						return t;
				throw new InvalidOperationException("Invalid thread id " + id);
			}
			catch
			{
				throw;
			}
		}

		void ClearEvalStatus()
		{
			foreach (CorProcess p in dbg.Processes)
			{
				if(p.Id == processId)
				{
					process = p;
					break;
				}
			}
		}

		public bool IsExternalCode(string fileName)
		{
			return string.IsNullOrWhiteSpace(fileName) || !documents.ContainsKey(fileName);
		}

		public string GetThreadName(CorThread thread)
		{
			// From http://social.msdn.microsoft.com/Forums/en/netfxtoolsdev/thread/461326fe-88bd-4a6b-82a9-1a66b8e65116
			try
			{
				CorReferenceValue refVal = thread.ThreadVariable.CastToReferenceValue();
				if(refVal.IsNull)
					return string.Empty;

				CorObjectValue val = refVal.Dereference().CastToObjectValue();
				if(val != null)
				{
					MetadataTypeInfo classType = val.ExactType.GetTypeInfo(this);
					// Loop through all private instance fields in the thread class
					foreach (MetadataFieldInfo fi in classType.GetFields())
					{
						if(fi.Name == "m_Name")
						{
							CorReferenceValue fieldValue = val.GetFieldValue(val.Class, fi.MetadataToken).CastToReferenceValue();

							if(fieldValue.IsNull)
								return string.Empty;
							else
								return fieldValue.Dereference().CastToStringValue().String;
						}
					}
				}
			} catch(Exception)
			{
				// Ignore
			}

			return string.Empty;
		}

		internal void ReplyEvent(EventRequest request, Action<Packet> action = null)
		{
			SendEvent(request.EventKind, request.RequestId, action);
		}

		internal void SendEvent(int eventKind, int requestId = 0, Action<Packet> action = null)
		{
			Packet packet = Packet.CreateEventPacket();
			packet.WriteByte(SuspendPolicy.ALL);
			packet.WriteInt(1); // event size

			packet.WriteByte(eventKind);
			packet.WriteInt(requestId);

			if(action != null)
			{
				action(packet);
			}

			myConnection.SendPacket(packet);
		}
	}
}