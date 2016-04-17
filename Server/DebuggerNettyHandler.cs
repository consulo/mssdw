using System;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using DotNetty.Buffers;
using Newtonsoft.Json;
using Consulo.Internal.Mssdw.Server.Event;
using Consulo.Internal.Mssdw.Server.Request;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class DebuggerNettyHandler : ChannelHandlerAdapter
	{
		private NettyClient client;

		private DebugSession debugSession;

		private Dictionary<string, Action<ClientMessage>> queries = new Dictionary<string, Action<ClientMessage>>();

		public DebuggerNettyHandler(DebugSession debugSession)
		{
			this.debugSession = debugSession;
		}

		public override void ChannelRegistered(DotNetty.Transport.Channels.IChannelHandlerContext context)
		{
			debugSession.Client = client = new NettyClient(context.Channel, this);
		}

		public override void ChannelUnregistered(DotNetty.Transport.Channels.IChannelHandlerContext context)
		{
			debugSession.Client = null;

			// force paused threads
			foreach (KeyValuePair<string, Action<ClientMessage>> keyValue in queries)
			{
				ClientMessage clientAnswer = new ClientMessage();
				clientAnswer.Id = keyValue.Key;
				clientAnswer.Continue = true;

				keyValue.Value(clientAnswer);
			}
		}

		public override void ChannelRead(IChannelHandlerContext context, object message)
		{
			IByteBuffer buffer = message as IByteBuffer;
			if(buffer != null)
			{
				Task.Run(async () =>
				{
					string jsonContext = buffer.ToString(Encoding.UTF8);

					//Console.WriteLine("receive: " + jsonContext);
					try
					{
						ClientMessage clientMessage = JsonConvert.DeserializeObject<ClientMessage>(jsonContext, new ClientMessageConverter());

						Action<ClientMessage> action;
						if(!queries.TryGetValue(clientMessage.Id, out action))
						{
							object messageObject = clientMessage.Object;
							if(messageObject is InsertBreakpointRequest)
							{
								InsertBreakpointRequestResult result = debugSession.InsertBreakpoint((InsertBreakpointRequest)messageObject);

								await SendMessage<InsertBreakpointRequestResult>(clientMessage, result);
							}
							else if(messageObject is GetThreadsRequest)
							{
								GetThreadsRequestResult result = new GetThreadsRequestResult();

								result.ActiveThreadId = debugSession.ActiveThread == null ? -1 : debugSession.ActiveThread.Id;
								foreach (CorThread thread in debugSession.Process.Threads)
								{
									result.Add(thread.Id);
								}

								await SendMessage<GetThreadsRequestResult>(clientMessage, result);
							}
							else if(messageObject is GetFramesRequest)
							{
								GetFramesRequestResult result = new GetFramesRequestResult();

								CorThread current = debugSession.Process.Threads.Where(x => x.Id == ((GetFramesRequest)messageObject).ThreadId).First();
								if(current != null)
								{
									IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
									foreach (CorFrame corFrame in frames)
									{
										AddFrame(debugSession, corFrame, result);
									}
								}
								await SendMessage<GetFramesRequestResult>(clientMessage, result);
							}
							else
							{
								Console.WriteLine("Uknoown object: " + messageObject.GetType());
							}
						}
						else
						{
							action(clientMessage);
						}
					}
					catch(Exception e)
					{
						Console.WriteLine("Erro with: " + jsonContext);
						Console.WriteLine(e.StackTrace);
					}
				});
			}
		}

		private async Task SendMessage<T>(ClientMessage clientMessage, T value) where T : class
		{
			ServerMessage<T> serverMessage = new ServerMessage<T>(value);

			serverMessage.Id = clientMessage.Id;
			await client.Write(serverMessage);
		}

		public override void ChannelReadComplete(IChannelHandlerContext context)
		{
			context.Flush();
		}

		public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
		{
			Console.WriteLine("Exception: " + exception);
		}

		public void PutWaiter(string id, Action<ClientMessage> action)
		{
			queries.Add(id, action);
		}

		internal static void AddFrame(DebugSession session, CorFrame frame, GetFramesRequestResult result)
		{
			// TODO: Fix remaining.
			uint address = 0;
			//string typeFQN;
			//string typeFullName;
			string addressSpace = "";
			string file = "";
			int line = 0;
			int endLine = 0;
			int column = 0;
			int endColumn = 0;
			string method = "";
			string lang = "";
			string module = "";
			string type = "";
			bool hasDebugInfo = false;
			bool hidden = false;
			bool external = true;

			if(frame.FrameType == CorFrameType.ILFrame)
			{
				if(frame.Function != null)
				{
					module = frame.Function.Module.Name;
					CorMetadataImport importer = new CorMetadataImport(frame.Function.Module);
					MethodInfo mi = importer.GetMethodInfo(frame.Function.Token);
					method = mi.DeclaringType.FullName + "." + mi.Name;
					type = mi.DeclaringType.FullName;
					addressSpace = mi.Name;

					var sp = GetSequencePoint(session, frame);
					if(sp != null)
					{
						line = sp.StartLine;
						column = sp.StartColumn;
						endLine = sp.EndLine;
						endColumn = sp.EndColumn;
						file = sp.Document.URL;
						address = (uint)sp.Offset;
					}

					object[] customAttributes = mi.GetCustomAttributes(true);

					if(session.IsExternalCode(file))
					{
						external = true;
					}
					else
					{
						/*if (session.Options.ProjectAssembliesOnly) {
							external = mi.GetCustomAttributes(true).Any(v =>
							v is System.Diagnostics.DebuggerHiddenAttribute ||
							v is System.Diagnostics.DebuggerNonUserCodeAttribute);
						} else */
						{
							external = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
						}
					}
					hidden = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
				}
				lang = "Managed";
				hasDebugInfo = true;
			}
			else if(frame.FrameType == CorFrameType.NativeFrame)
			{
				frame.GetNativeIP(out address);
				method = "<Unknown>";
				lang = "Native";
			}
			else if(frame.FrameType == CorFrameType.InternalFrame)
			{
				switch(frame.InternalFrameType)
				{
					case CorDebugInternalFrameType.STUBFRAME_M2U:
						method = "[Managed to Native Transition]";
						break;
					case CorDebugInternalFrameType.STUBFRAME_U2M:
						method = "[Native to Managed Transition]";
						break;
					case CorDebugInternalFrameType.STUBFRAME_LIGHTWEIGHT_FUNCTION:
						method = "[Lightweight Method Call]";
						break;
					case CorDebugInternalFrameType.STUBFRAME_APPDOMAIN_TRANSITION:
						method = "[Application Domain Transition]";
						break;
					case CorDebugInternalFrameType.STUBFRAME_FUNC_EVAL:
						method = "[Function Evaluation]";
						break;
				}
			}

			if(method == null)
				method = "<Unknown>";

			result.Add(file, line, method);
		}

		private const int SpecialSequencePoint = 0xfeefee;

		public static SequencePoint GetSequencePoint(DebugSession session, CorFrame frame)
		{
			ISymbolReader reader = session.GetReaderForModule(frame.Function.Module.Name);
			if(reader == null)
				return null;

			ISymbolMethod met = reader.GetMethod(new SymbolToken(frame.Function.Token));
			if(met == null)
				return null;

			int SequenceCount = met.SequencePointCount;
			if(SequenceCount <= 0)
				return null;

			CorDebugMappingResult mappingResult;
			uint ip;
			frame.GetIP(out ip, out mappingResult);
			if(mappingResult == CorDebugMappingResult.MAPPING_NO_INFO || mappingResult == CorDebugMappingResult.MAPPING_UNMAPPED_ADDRESS)
				return null;

			int[] offsets = new int[SequenceCount];
			int[] lines = new int[SequenceCount];
			int[] endLines = new int[SequenceCount];
			int[] columns = new int[SequenceCount];
			int[] endColumns = new int[SequenceCount];
			ISymbolDocument[] docs = new ISymbolDocument[SequenceCount];
			met.GetSequencePoints(offsets, docs, lines, columns, endLines, endColumns);

			if((SequenceCount > 0) && (offsets[0] <= ip))
			{
				int i;
				for(i = 0; i < SequenceCount; ++i)
				{
					if(offsets[i] >= ip)
					{
						break;
					}
				}

				if((i == SequenceCount) || (offsets[i] != ip))
				{
					--i;
				}

				if(lines[i] == SpecialSequencePoint)
				{
					int j = i;
					// let's try to find a sequence point that is not special somewhere earlier in the code
					// stream.
					while(j > 0)
					{
						--j;
						if(lines[j] != SpecialSequencePoint)
						{
							return new SequencePoint()
							{
								IsSpecial = true,
								Offset = offsets[j],
								StartLine = lines[j],
								EndLine = endLines[j],
								StartColumn = columns[j],
								EndColumn = endColumns[j],
								Document = docs[j]
							};
						}
					}
					// we didn't find any non-special seqeunce point before current one, let's try to search
					// after.
					j = i;
					while(++j < SequenceCount)
					{
						if(lines[j] != SpecialSequencePoint)
						{
							return new SequencePoint()
							{
								IsSpecial = true,
								Offset = offsets[j],
								StartLine = lines[j],
								EndLine = endLines[j],
								StartColumn = columns[j],
								EndColumn = endColumns[j],
								Document = docs[j]
							};
						}
					}

					// Even if sp is null at this point, it's a valid scenario to have only special sequence
					// point in a function.  For example, we can have a compiler-generated default ctor which
					// doesn't have any source.
					return null;
				}
				else
				{
					return new SequencePoint()
					{
						IsSpecial = false,
						Offset = offsets[i],
						StartLine = lines[i],
						EndLine = endLines[i],
						StartColumn = columns[i],
						EndColumn = endColumns[i],
						Document = docs[i]
					};
				}
			}
			return null;
		}
	}
}