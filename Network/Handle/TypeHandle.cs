using System;
using System.Collections.Generic;
using System.Linq;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class TypeHandle
	{
		private const int GetInfo = 1;
		private const int GetMethods = 2;
		private const int GetFields = 3;
		private const int GetValue = 4;
		private const int GetProperties = 9;

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			TypeRef typeRef = packet.ReadTypeRef();
			MetadataTypeInfo typeInfo = null;
			CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(typeRef.ModuleName);
			if(metadataForModule != null)
			{
				typeInfo = metadataForModule.CreateMetadataTypeInfo(typeRef);
			}

			switch(packet.Command)
			{
				case GetInfo:
				{
					packet.WriteString(typeInfo == null ? "" : typeInfo.Namespace);
					packet.WriteString(typeInfo == null ? "" : typeInfo.Name);
					packet.WriteString(typeInfo == null ? "" : typeInfo.FullName);
					packet.WriteTypeRef(typeInfo == null ? null : typeInfo.BaseType(debugSession));
					packet.WriteInt(typeInfo == null ? 0 : (int) typeInfo.Attributes);
					packet.WriteBool(typeInfo != null && typeInfo.IsArray);
					break;
				}
				case GetMethods:
				{
					if(typeInfo == null)
					{
						packet.WriteInt(0);
					}
					else
					{
						MetadataMethodInfo[] methodInfos = typeInfo.GetMethods();
						packet.WriteInt(methodInfos.Length);
						foreach (MetadataMethodInfo methodInfo in methodInfos)
						{
							packet.WriteInt(methodInfo.MetadataToken);
						}
					}
					break;
				}
				case GetFields:
				{
					if(typeInfo == null)
					{
						packet.WriteInt(0);
					}
					else
					{
						MetadataFieldInfo[] fieldInfos = typeInfo.GetFields();
						packet.WriteInt(fieldInfos.Length);
						foreach (MetadataFieldInfo fieldInfo in fieldInfos)
						{
							packet.WriteInt(fieldInfo.MetadataToken);
							packet.WriteString(fieldInfo.Name);
							packet.WriteTypeRef(new TypeRef(fieldInfo.FieldType));
							packet.WriteInt((int) fieldInfo.Attributes);
						}
					}
					break;
				}
				case GetValue:
				{
					if(typeInfo == null)
					{
						packet.WriteValue(null, debugSession);
					}
					else
					{
						int fieldId = packet.ReadInt();
						int threadId = packet.ReadInt();
						int stackFrameId = packet.ReadInt();

						MetadataFieldInfo metadataFieldInfo = typeInfo.GetFields().Where(x => x.MetadataToken == fieldId).First();
						CorThread corThread = debugSession.GetThread(threadId);

						IEnumerable<CorFrame> frames = DebugSession.GetFrames(corThread);
						int i = 0;
						CorFrame corFrame = null;
						foreach (CorFrame frame in frames)
						{
							if(i == stackFrameId)
							{
								corFrame = frame;
								break;
							}
							i++;
						}

						if(typeInfo.ReallyIsEnum && metadataFieldInfo.IsConstant)
						{
							var value = typeInfo.EnumValues.Where(arg => arg.Key == metadataFieldInfo.Name).ToArray();
							if(value.Length == 0)
							{
								packet.WriteValue(null, debugSession);
							}
							else
							{
								KeyValuePair<string, ulong> keyValue = value[0];
								packet.WriteByte((int) CorElementType.ELEMENT_TYPE_U8);
								packet.WriteLong((long) keyValue.Value);
							}
						}
						else
						{
							packet.WriteValue(typeInfo.GetFieldValue(metadataFieldInfo, corFrame), debugSession);
						}
					}
					break;
				}
				case GetProperties:
				{
					if(typeInfo == null)
					{
						packet.WriteInt(0);
					}
					else
					{
						MetadataPropertyInfo[] propertyInfos = typeInfo.GetProperties();
						packet.WriteInt(propertyInfos.Length);
						foreach (MetadataPropertyInfo propertyInfo in propertyInfos)
						{
							packet.WriteInt(propertyInfo.MetadataToken);
							packet.WriteString(propertyInfo.Name);
							packet.WriteInt((int) propertyInfo.Attributes);
							MetadataMethodInfo getMethod = propertyInfo.GetGetMethod();
							packet.WriteInt(getMethod == null ? 0 : getMethod.MetadataToken);
							MetadataMethodInfo setMethod = propertyInfo.GetSetMethod();
							packet.WriteInt(setMethod == null ? 0 : setMethod.MetadataToken);
						}
					}
					break;
				}
				default:
					return false;
			}
			return true;
		}
	}
}