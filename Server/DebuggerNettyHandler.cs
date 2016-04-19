using System;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DotNetty.Transport.Channels;
using DotNetty.Buffers;
using Consulo.Internal.Mssdw.Server.Event;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using Consulo.Internal.Mssdw.Server.Request;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

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

			StopWaitors();
		}

		private void StopWaitors()
		{
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
							object temp = null;

							try
							{
								if(messageObject is InsertBreakpointRequest)
								{
									InsertBreakpointRequestResult result = debugSession.InsertBreakpoint((InsertBreakpointRequest)messageObject);

									temp = result;
								}
								else if(messageObject is GetThreadsRequest)
								{
									GetThreadsRequestResult result = new GetThreadsRequestResult();

									foreach (CorThread thread in debugSession.Process.Threads)
									{
										result.Add(thread.Id, GetThreadName(thread));
									}

									temp = result;
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

									temp = result;
								}
								else if(messageObject is GetMethodInfoRequest)
								{
									GetMethodInfoRequest request = (GetMethodInfoRequest) messageObject;

									GetMethodInfoRequestResult result = new GetMethodInfoRequestResult();
									result.Name = "<unknown>";

									CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(request.Type.ModuleToken);
									if(metadataForModule != null)
									{
										MethodInfo methodInfo = metadataForModule.GetMethodInfo(request.FunctionToken);
										if(methodInfo != null)
										{
											result.Name = methodInfo.Name;
											foreach (ParameterInfo o in methodInfo.GetParameters())
											{
												string parameterName = o.Name;

												result.AddParameter(parameterName, new TypeRef(o.ParameterType));
											}
										}
									}

									temp = result;
								}
								else if(messageObject is GetTypeInfoRequest)
								{
									GetTypeInfoRequest request = (GetTypeInfoRequest) messageObject;

									GetTypeInfoRequestResult result = new GetTypeInfoRequestResult();

									result.Name = "<unknown>";

									CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(request.Type.ModuleToken);
									if(metadataForModule != null)
									{
										Type type = metadataForModule.GetType(request.Type.ClassToken);
										if(type != null)
										{
											result.Name = type.Name;
										}
									}

									temp = result;
								}
								else if(messageObject is GetArgumentRequest)
								{
									GetArgumentRequest argumentRequest = (GetArgumentRequest)messageObject;
									CorThread current = debugSession.Process.Threads.Where(x => x.Id == argumentRequest.ThreadId).FirstOrDefault();
									if(current != null)
									{
										IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
										int i = 0;
										CorFrame corFrame = null;
										foreach (CorFrame frame in frames)
										{
											if(i == argumentRequest.StackFrameIndex)
											{
												corFrame = frame;
												break;
											}
											i++;
										}

										if(corFrame != null)
										{
											CorValue value = corFrame.GetArgument(argumentRequest.Index);

											temp = CreateValueResult(value == null ? -1 : value.Id, value);
										}
									}
									if(temp == null)
									{
										temp = new UnknownValueResult();
									}
								}
								else if(messageObject is GetLocalsRequest)
								{
									GetLocalsRequestResult result = new GetLocalsRequestResult();

									GetLocalsRequest localsRequest = (GetLocalsRequest)messageObject;
									CorThread current = debugSession.Process.Threads.Where(x => x.Id == localsRequest.ThreadId).FirstOrDefault();
									if(current != null)
									{
										IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
										int i = 0;
										CorFrame corFrame = null;
										foreach (CorFrame frame in frames)
										{
											if(i == localsRequest.StackFrameIndex)
											{
												corFrame = frame;
												break;
											}
											i++;
										}

										if(corFrame != null)
										{
											uint offset;
											CorDebugMappingResult mr;

											corFrame.GetIP(out offset, out mr);

											ISymbolMethod met = corFrame.Function.GetSymbolMethod(debugSession);

											ISymbolScope scope = met.RootScope;

											collectLocals(scope, (int) offset, result);
										}
									}

									temp = result;
								}
								else if(messageObject is ContinueRequest)
								{
									debugSession.Process.Continue(false);

									temp = new ContinueRequestResult();
								}
							}
							catch(Exception e)
							{
								Console.WriteLine(e.Message);
								Console.WriteLine(e.StackTrace);
							}

							if(temp == null)
							{
								temp = new BadRequestResult();
								Console.WriteLine("Bad handle for object: " + (messageObject == null ? "null" : messageObject.GetType().FullName));
							}

							await SendMessage<object>(clientMessage, temp);
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

		private static void collectLocals(ISymbolScope scope, int offset, GetLocalsRequestResult result)
		{
			ISymbolVariable[] locals = scope.GetLocals();
			foreach (ISymbolVariable local in locals)
			{
				int index = local.AddressField1;
				//if (local.StartOffset <= offset && local.EndOffset >= offset)
				{
					result.Add(index, local.Name);
				}
			}

			foreach (ISymbolScope o in scope.GetChildren())
			{
				collectLocals(o, offset, result);
			}
		}

		private static object CreateValueResult(int id, CorValue corValue)
		{
			if(corValue == null)
			{
				return new UnknownValueResult();
			}

			CorReferenceValue toReferenceValue = corValue.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				return CreateValueResult(id, toReferenceValue.Dereference());
			}

			CorElType corValueType = corValue.Type;
			switch(corValueType)
			{
				case CorElType.ELEMENT_TYPE_STRING:
					return new StringValueResult(id, corValue.CastToStringValue());
				case CorElType.ELEMENT_TYPE_BOOLEAN:
					return new BooleanValueResult(id, corValue.CastToGenericValue());
			}
			return new UnknownValueResult();
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
			StopWaitors();
		}

		public void PutWaiter(string id, Action<ClientMessage> action)
		{
			queries.Add(id, action);
		}

		string GetThreadName(CorThread thread)
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
					Type classType = val.ExactType.GetTypeInfo(debugSession);
					// Loop through all private instance fields in the thread class
					foreach (FieldInfo fi in classType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
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

		internal static void AddFrame(DebugSession session, CorFrame frame, GetFramesRequestResult result)
		{
			uint address = 0;
			//string typeFQN;
			//string typeFullName;
			//string addressSpace = "";
			string file = "";
			int line = 0;
			int endLine = 0;
			int column = 0;
			int endColumn = 0;
			//string method = "";
			//string lang = "";
			//string module = "";
			//string type = "";
			bool hasDebugInfo = false;
			//bool hidden = false;
			//bool external = true;

			int moduleToken = -1;
			int classToken = -1;
			int functionToken = -1;

			if(frame.FrameType == CorFrameType.ILFrame)
			{
				if(frame.Function != null)
				{
					moduleToken = frame.Function.Module.Token;
					classToken = frame.Function.Class.Token;
					functionToken = frame.FunctionToken;

					//module = frame.Function.Module.Name;
					//CorMetadataImport importer = new CorMetadataImport(frame.Function.Module);
					//MethodInfo mi = importer.GetMethodInfo(frame.Function.Token);
					//method = mi.DeclaringType.FullName + "." + mi.Name;
					//type = mi.DeclaringType.FullName;
					//addressSpace = mi.Name;

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

					///object[] customAttributes = mi.GetCustomAttributes(true);

					//if(session.IsExternalCode(file))
					//{
					//	external = true;
					//}
					//else
					//{
					/*if (session.Options.ProjectAssembliesOnly) {
						external = mi.GetCustomAttributes(true).Any(v =>
						v is System.Diagnostics.DebuggerHiddenAttribute ||
						v is System.Diagnostics.DebuggerNonUserCodeAttribute);
					} else */
					//{
					//	external = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
					//}
					//}
					//hidden = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
				}
				hasDebugInfo = true;
			}
			else if(frame.FrameType == CorFrameType.NativeFrame)
			{
				frame.GetNativeIP(out address);
			}
			else if(frame.FrameType == CorFrameType.InternalFrame)
			{
			}

			result.Add(file, line, column, new TypeRef(moduleToken, classToken), functionToken);
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