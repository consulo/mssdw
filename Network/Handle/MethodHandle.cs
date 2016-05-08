using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.Extensions;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class MethodHandle
	{
		private const int GetName = 1;
		private const int GetParamInfo = 4;
		private const int GetLocalsInfo = 5;
		private const int GetInfo = 6;

		public class LocalInfo
		{
			public int Index;
			public string Name;
			public TypeRef Type;

			public LocalInfo(int index, MetadataTypeInfo type, string name)
			{
				Index = index;
				Name = name;
				Type = new TypeRef(type);
			}
		}

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			TypeRef typeRef = packet.ReadTypeRef();
			int methodId = packet.ReadInt();

			MetadataMethodInfo methodInfo = null;
			CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(typeRef.GetModuleName());
			if(metadataForModule != null)
			{
				methodInfo = metadataForModule.GetMethodInfo(methodId);
			}

			switch(packet.Command)
			{
				case GetName:
					packet.WriteString(methodInfo == null ? "<invalid>" : methodInfo.Name);
					break;
				case GetParamInfo:
					if(methodInfo == null)
					{
						packet.WriteInt(0); // call conversion
						packet.WriteInt(0); // param count
						packet.WriteInt(0); // generic param count
						packet.WriteTypeRef(null);
					}
					else
					{
						MetadataParameterInfo[] parameters = methodInfo.GetParameters();
						packet.WriteInt(0);
						packet.WriteInt(parameters.Length);
						packet.WriteInt(methodInfo.GetGenericArgumentNames().Length);
						packet.WriteTypeRef(new TypeRef(methodInfo.ReturnType));
						foreach (MetadataParameterInfo parameter in parameters)
						{
							packet.WriteTypeRef(new TypeRef(parameter.ParameterType));
							packet.WriteString(parameter.Name);
						}
					}
					break;
				case GetInfo:
					packet.WriteInt(methodInfo == null ? 0 : (int)methodInfo.Attributes);
					packet.WriteInt(methodInfo == null ? 0 : (int)methodInfo.ImplAttributes);
					break;
				case GetLocalsInfo:
					int threadId = packet.ReadInt();
					int stackFrameId = packet.ReadInt();

					if(methodInfo == null)
					{
						packet.WriteInt(0); // locals size
					}
					else
					{
						CorFrame corFrame = null;
						CorThread current = debugSession.Process.Threads.Where(x => x.Id == threadId).FirstOrDefault();
						if(current != null)
						{
							IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
							int i = 0;
							foreach (CorFrame frame in frames)
							{
								if(i == stackFrameId)
								{
									corFrame = frame;
									break;
								}
							}
						}
						if(corFrame == null)
						{
							packet.WriteInt(0); // locals size
							return true;
						}

						uint offset;
						CorDebugMappingResult mr;

						corFrame.GetIP(out offset, out mr);

						CorMetadataImport module = debugSession.GetMetadataForModule(ModuleNameRegistrator.Get(typeRef.ModuleNameId));
						ISymbolMethod met = module.Module.GetFunctionFromToken(methodId).GetSymbolMethod(debugSession);

						ISymbolScope scope = met.RootScope;

						List<LocalInfo> locals = new List<LocalInfo>();

						collectLocals(scope, module, (int) offset, locals);

						packet.WriteInt(locals.Count);
						foreach (LocalInfo local in locals)
						{
							packet.WriteInt(local.Index);
							packet.WriteString(local.Name);
							packet.WriteTypeRef(local.Type);
						}
					}
					break;
				default:
					return false;
			}

			return true;
		}

		private static void collectLocals(ISymbolScope scope, CorMetadataImport metadataImport, int offset, List<LocalInfo> result)
		{
			ISymbolVariable[] locals = scope.GetLocals();
			foreach (ISymbolVariable local in locals)
			{
				int index = local.AddressField1;
				//if (local.StartOffset <= offset && local.EndOffset >= offset)
				{
					MetadataTypeInfo type = null;
					byte[] signature = local.GetSignature();
					unsafe
					{
						fixed (byte* p = signature)
						{
							IntPtr ptr = (IntPtr) p;
							type = MetadataHelperFunctionsExtensions.ReadType(metadataImport, metadataImport.m_importer, ref ptr);
						}
					}

					result.Add(new LocalInfo(index, type, local.Name));
				}
			}

			foreach (ISymbolScope o in scope.GetChildren())
			{
				collectLocals(o, metadataImport, offset, result);
			}
		}
	}
}