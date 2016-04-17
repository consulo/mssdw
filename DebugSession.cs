using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorSymbolStore;
using System.Threading.Tasks;
using Consulo.Internal.Mssdw.Server;
using Consulo.Internal.Mssdw.Server.Event;
using Consulo.Internal.Mssdw.Server.Request;

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

		class DocInfo
		{
			public ISymbolReader Reader;
			public ISymbolDocument Document;
			public CorModule Module;
		}

		private readonly object terminateLock = new object();
		private readonly object debugLock = new object();
		private readonly Dictionary<string, DocInfo> documents = new Dictionary<string, DocInfo>(StringComparer.CurrentCultureIgnoreCase);
		private readonly Dictionary<string, ModuleInfo> modules = new Dictionary<string, ModuleInfo>();
		private readonly SymbolBinder symbolBinder = new SymbolBinder();
		readonly Dictionary<CorBreakpoint, InsertBreakpointRequestResult> breakpoints = new Dictionary<CorBreakpoint, InsertBreakpointRequestResult>();

		public event Action<DebugSession> OnProcessExit = delegate(DebugSession arg1)
		{
		};

		private CorProcess process;
		private CorDebugger dbg;
		private int processId;
		private bool evaluating;
		private CorThread activeThread;
		private CorStepper stepper;
		private bool autoStepInto;

		public NettyClient Client
		{
			get;
			set;
		}

		public CorThread ActiveThread
		{
			get
			{
				return activeThread;
			}
		}

		public void Start(List<string> args)
		{
			string command = args[0];
			string commandLine = string.Join(" ", args);
			Console.WriteLine("running: " + commandLine);
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

			//foreach (KeyValuePair<string, string> var in startInfo.EnvironmentVariables)
			//   env[var.Key] = var.Value;

			int flags = 0;
			//if (!startInfo.UseExternalConsole)
			/*{
				flags = (int)CreationFlags.CREATE_NO_WINDOW;
				flags |= DebuggerExtensions.CREATE_REDIRECT_STD;
			}*/

			process = dbg.CreateProcess(command, commandLine, parentDirectory.FullName, env, flags);
			processId = process.Id;
			Console.WriteLine("processId: " + processId);

			process.OnCreateProcess += new CorProcessEventHandler(OnCreateProcess);
			process.OnCreateAppDomain += new CorAppDomainEventHandler(OnCreateAppDomain);
			/*  process.OnAssemblyLoad += new CorAssemblyEventHandler(OnAssemblyLoad);
			  process.OnAssemblyUnload += new CorAssemblyEventHandler(OnAssemblyUnload);
			  process.OnCreateThread += new CorThreadEventHandler(OnCreateThread);
			  process.OnThreadExit += new CorThreadEventHandler(OnThreadExit);*/
			process.OnModuleLoad += new CorModuleEventHandler(OnModuleLoadImpl);
			process.OnModuleUnload += new CorModuleEventHandler(OnModuleUnload);
			process.OnProcessExit += new CorProcessEventHandler(OnProcessExitImpl);
			/*  process.OnUpdateModuleSymbols += new UpdateModuleSymbolsEventHandler(OnUpdateModuleSymbols);
			  process.OnDebuggerError += new DebuggerErrorEventHandler(OnDebuggerError);*/
			process.OnBreakpoint += new BreakpointEventHandler(OnBreakpoint);
			/*  process.OnStepComplete += new StepCompleteEventHandler(OnStepComplete);
			  process.OnBreak += new CorThreadEventHandler(OnBreak);
			  process.OnNameChange += new CorThreadEventHandler(OnNameChange);
			  process.OnEvalComplete += new EvalEventHandler(OnEvalComplete);
			  process.OnEvalException += new EvalEventHandler(OnEvalException);
			  process.OnLogMessage += new LogMessageEventHandler(OnLogMessage);
			  process.OnException2 += new CorException2EventHandler(OnException2);
	*/
			//process.RegisterStdOutput(OnStdOutput);

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

		void SetActiveThread (CorThread t)
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

		public InsertBreakpointRequestResult InsertBreakpoint(InsertBreakpointRequest request)
		{
			InsertBreakpointRequestResult result = new InsertBreakpointRequestResult(request);

			DocInfo doc;
			if(!documents.TryGetValue(System.IO.Path.GetFullPath(request.FilePath), out doc))
			{
				result.SetStatus(BreakEventStatus.NotBound, null);
				return result;
			}

			int line;
			try
			{
				line = doc.Document.FindClosestLine(request.Line);
			} catch
			{
				// Invalid line
				result.SetStatus(BreakEventStatus.Invalid, null);
				return result;
			}
			ISymbolMethod met = null;
			if(doc.Reader is ISymbolReader2)
			{
				var methods = ((ISymbolReader2)doc.Reader).GetMethodsFromDocumentPosition(doc.Document, line, 0);
				if(methods != null && methods.Any())
				{
					if(methods.Count() == 1)
					{
						met = methods[0];
					}
					else
					{
						int deepest = -1;
						foreach (var method in methods)
						{
							var firstSequence = method.GetSequencePoints().FirstOrDefault((sp) => sp.StartLine != 0xfeefee);
							if(firstSequence != null && firstSequence.StartLine >= deepest)
							{
								deepest = firstSequence.StartLine;
								met = method;
							}
						}
					}
				}
			}
			if(met == null)
			{
				met = doc.Reader.GetMethodFromDocumentPosition(doc.Document, line, 0);
			}
			if(met == null)
			{
				result.SetStatus(BreakEventStatus.Invalid, null);
				return result;
			}

			int offset = -1;
			int firstSpInLine = -1;
			foreach (SequencePoint sp in met.GetSequencePoints())
			{
				if(sp.IsInside(doc.Document.URL, line, request.Column))
				{
					offset = sp.Offset;
					break;
				}
				else if(firstSpInLine == -1
				&& sp.StartLine == line
				&& sp.Document.URL.Equals(doc.Document.URL, StringComparison.OrdinalIgnoreCase))
				{
					firstSpInLine = sp.Offset;
				}
			}
			if(offset == -1)
			{
				//No exact match? Use first match in that line
				offset = firstSpInLine;
			}
			if(offset == -1)
			{
				result.SetStatus(BreakEventStatus.Invalid, null);
				return result;
			}

			CorFunction func = doc.Module.GetFunctionFromToken(met.Token.GetToken());
			CorFunctionBreakpoint corBp = func.ILCode.CreateBreakpoint(offset);
			corBp.Activate(true);
			breakpoints[corBp] = result;

			//result.Handle = corBp;
			result.Status = BreakEventStatus.Bound;
			return result;
		}

		void OnBreakpoint (object sender, CorBreakpointEventArgs e)
		{
			lock (debugLock)
			{
				if(evaluating)
				{
					e.Continue = true;
					return;
				}
			}

			InsertBreakpointRequestResult binfo;
			if(breakpoints.TryGetValue(e.Breakpoint, out binfo))
			{
				e.Continue = true;
				InsertBreakpointRequest bp = (InsertBreakpointRequest)binfo.Request;

				binfo.IncrementHitCount();
				//if (!binfo.HitCountReached)
				//   return;

				/* if (!string.IsNullOrEmpty(bp.ConditionExpression)) {
					 string res = EvaluateExpression(e.Thread, bp.ConditionExpression);
					 if (bp.BreakIfConditionChanges) {
						 if (res == bp.LastConditionValue)
							 return;
						 bp.LastConditionValue = res;
					 } else {
						 if (res != null && res.ToLower() == "false")
							 return;
					 }
				 }*/

				/*if ((bp.HitAction & HitAction.CustomAction) != HitAction.None) {
					// If custom action returns true, execution must continue
					if (binfo.RunCustomBreakpointAction(bp.CustomActionId))
						return;
				}

				if ((bp.HitAction & HitAction.PrintExpression) != HitAction.None) {
					string exp = EvaluateTrace(e.Thread, bp.TraceExpression);
					binfo.UpdateLastTraceValue(exp);
				}

				if ((bp.HitAction & HitAction.Break) == HitAction.None)
					return;*/
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

			ClientMessage result = binfo != null ? Notify(new OnBreakpointFire(binfo.Request.FilePath, binfo.Request.Line)) : null;
			if(result != null)
			{
				e.Continue = result.Continue;
			}
			else
			{
				e.Continue = true;
			}
		}

		private void OnModuleLoadImpl(object sender, CorModuleEventArgs e)
		{
			CorMetadataImport mi = new CorMetadataImport(e.Module);

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

			ClientMessage result = Notify(new OnModuleLoadEvent(file));
			if(result != null)
			{
				e.Continue = result.Continue;
			}
			else
			{
				e.Continue = true;
			}
		}

		private void OnModuleUnload (object sender, CorModuleEventArgs e)
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

		public void OnProcessExitImpl(object sender, CorProcessEventArgs e)
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
				}
			}
			OnProcessExit(this);
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

		internal CorMetadataImport GetMetadataForModule (string file)
		{
			lock (documents)
			{
				ModuleInfo mod;
				if(!modules.TryGetValue(System.IO.Path.GetFullPath(file), out mod))
					return null;
				return mod.Importer;
			}
		}

		internal ISymbolReader GetReaderForModule (string file)
		{
			lock (documents)
			{
				ModuleInfo mod;
				if(!modules.TryGetValue(System.IO.Path.GetFullPath(file), out mod))
					return null;
				return mod.Reader;
			}
		}

		public bool IsExternalCode (string fileName)
		{
			return string.IsNullOrWhiteSpace(fileName)
			|| !documents.ContainsKey(fileName);
		}

		private ClientMessage Notify<T>(T value) where T : class
		{
			if(Client != null)
			{
				Task<ClientMessage> task = Client.Notify(value);
				task.Wait();
				return task.Result;
			}
			else
			{
				return null;
			}
		}

		/*public void OnStdOutput (object sender, CorTargetOutputEventArgs e) {
			if (e.IsStdError) {
				Console.Error.WriteLine(e.Text);
			}
			else {
				Console.WriteLine(e.Text);
			}
		}*/
	}
}