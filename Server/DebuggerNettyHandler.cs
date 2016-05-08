using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Threading.Tasks;
using Consulo.Internal.Mssdw.Server.Event;
using Consulo.Internal.Mssdw.Server.Request;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.Extensions;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Consulo.Internal.Mssdw.Server
{
	[Obsolete]
	public class DebuggerNettyHandler
	{
		private DebugSession debugSession;

		private Dictionary<string, Action<ClientMessage>> queries = new Dictionary<string, Action<ClientMessage>>();

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

		public void ChannelRead()
		{
			object buffer = null;
			if(buffer != null)
			{
				Task.Run(async () =>
				{
					string jsonContext = null;

					//Console.WriteLine("receive: " + jsonContext);
					try
					{
						ClientMessage clientMessage = null;

						Action<ClientMessage> action;
						if(!queries.TryGetValue(clientMessage.Id, out action))
						{
							object messageObject = clientMessage.Object;
							object temp = null;

							try
							{
								if(messageObject is GetMethodInfoRequest)
								{
									GetMethodInfoRequest request = (GetMethodInfoRequest) messageObject;

									GetMethodInfoRequestResult result = new GetMethodInfoRequestResult();
									result.Name = "<unknown>";

									CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(request.Type.GetModuleName());
									if(metadataForModule != null)
									{
										MetadataMethodInfo methodInfo = metadataForModule.GetMethodInfo(request.FunctionToken);
										if(methodInfo != null)
										{
											result.Name = methodInfo.Name;
											result.Attributes = (int) methodInfo.Attributes;
											foreach (MetadataParameterInfo o in methodInfo.GetParameters())
											{
												string parameterName = o.Name;

												result.AddParameter(parameterName, new TypeRef(o.ParameterType));
											}
										}
									}

									temp = result;
								}
								else if(messageObject is GetFieldInfoRequest)
								{
									GetFieldInfoRequest request = (GetFieldInfoRequest) messageObject;

									GetFieldInfoRequestResult result = new GetFieldInfoRequestResult();

									MetadataFieldInfo field = SearchUtil.FindField(debugSession, request.Type, request.Token);
									if(field != null)
									if(field.MetadataToken == request.Token)
									{
										result.Attributes = (int) field.Attributes;
										result.Type = new TypeRef(field.FieldType);
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

											temp = CreateValueResult(value, value);
										}
									}
									if(temp == null)
									{
										temp = new UnknownValueResult("temp = null");
									}
								}
								else if(messageObject is GetFieldValueRequest)
								{
									GetFieldValueRequest request = (GetFieldValueRequest)messageObject;

									MetadataFieldInfo field = SearchUtil.FindField(debugSession, request.Type, request.FieldToken);
									if(field != null)
									{
										if(request.ObjectId == 0)
										{
											Console.WriteLine(field.Name);
											MetadataTypeInfo type = field.FieldType;
											CorValue value = type.GetFieldValue(field, null);
											temp = CreateValueResult(value, value);
										}
										else
										{
											CorValue cor = CorValueRegistrator.Get(request.ObjectId);
											if(cor is CorObjectValue)
											{
												CorObjectValue objectValue = (CorObjectValue) cor;
												if(objectValue.Address == 0)
												{
													temp = new NullValueResult();
												}
												else
												{
													CorClass corClass = objectValue.Class;
													int fieldToken = request.FieldToken;
													CorValue value = objectValue.GetFieldValue(corClass, fieldToken);
													temp = CreateValueResult(value, value);
												}
											}
										}
									}
								}
								else if(messageObject is GetOrSetArrayValueAtRequest)
								{
									GetOrSetArrayValueAtRequest request = (GetOrSetArrayValueAtRequest)messageObject;

									CorArrayValue cor = CorValueRegistrator.Get(request.ObjectId) as CorArrayValue;
									if(cor != null)
									{
										CorValue value = cor.GetElement(new int[]{request.Index});
										temp = CreateValueResult(value);
									}
								}
								else if(messageObject is GetLocalValueRequest)
								{
									GetLocalValueRequest localRequest = (GetLocalValueRequest)messageObject;
									CorThread current = debugSession.Process.Threads.Where(x => x.Id == localRequest.ThreadId).FirstOrDefault();
									if(current != null)
									{
										IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
										int i = 0;
										CorFrame corFrame = null;
										foreach (CorFrame frame in frames)
										{
											if(i == localRequest.StackFrameIndex)
											{
												corFrame = frame;
												break;
											}
											i++;
										}

										if(corFrame != null)
										{
											CorValue value = corFrame.GetLocalVariable(localRequest.Index);

											temp = CreateValueResult(value, value);
										}
									}

									if(temp == null)
									{
										temp = new UnknownValueResult("temp = null");
									}
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


		private object CreateValueResult(CorValue originalValue)
		{
			return CreateValueResult(originalValue, originalValue);
		}

		private object CreateValueResult(CorValue originalValue, CorValue corValue)
		{
			if(corValue == null)
			{
				return new UnknownValueResult("corValue = null");
			}

			CorReferenceValue toReferenceValue = corValue.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				if(toReferenceValue.IsNull)
				{
					return new NullValueResult();
				}
				return CreateValueResult(originalValue, toReferenceValue.Dereference());
			}

			CorElType corValueType = corValue.Type;
			switch(corValueType)
			{
				case CorElType.ELEMENT_TYPE_CHAR:
					return new CharValueResult(originalValue, corValue.CastToGenericValue());
				case CorElType.ELEMENT_TYPE_I:
				case CorElType.ELEMENT_TYPE_U:
				case CorElType.ELEMENT_TYPE_I1:
				case CorElType.ELEMENT_TYPE_U1:
				case CorElType.ELEMENT_TYPE_I2:
				case CorElType.ELEMENT_TYPE_U2:
				case CorElType.ELEMENT_TYPE_I4:
				case CorElType.ELEMENT_TYPE_U4:
				case CorElType.ELEMENT_TYPE_I8:
				case CorElType.ELEMENT_TYPE_U8:
				case CorElType.ELEMENT_TYPE_R4:
				case CorElType.ELEMENT_TYPE_R8:
					return new NumberValueResult(originalValue, corValueType, corValue.CastToGenericValue());
				case CorElType.ELEMENT_TYPE_VOID:
					return new NullValueResult();
				case CorElType.ELEMENT_TYPE_CLASS:
				case CorElType.ELEMENT_TYPE_VALUETYPE:
					return new ObjectValueResult(originalValue, debugSession, corValue.CastToObjectValue());
				case CorElType.ELEMENT_TYPE_STRING:
					return new StringValueResult(originalValue, corValue.CastToStringValue());
				case CorElType.ELEMENT_TYPE_BOOLEAN:
					return new BooleanValueResult(originalValue, corValue.CastToGenericValue());
				case CorElType.ELEMENT_TYPE_SZARRAY:
					return new ArrayValueResult(originalValue, debugSession, corValue.CastToArrayValue());
				default:
					return new UnknownValueResult("corValueType: " + string.Format("{0:X}", corValueType));
			}
		}

		public void PutWaiter(string id, Action<ClientMessage> action)
		{
			queries.Add(id, action);
		}
	}
}