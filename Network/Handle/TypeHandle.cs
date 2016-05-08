using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class TypeHandle
	{
		private const int GetInfo = 1;
		private const int GetMethods = 2;
		private const int GetFields = 3;

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			TypeRef typeRef = packet.ReadTypeRef();
			MetadataTypeInfo typeInfo = null;
			CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(typeRef.GetModuleName());
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
					MetadataTypeInfo baseTypeInfo = typeInfo == null ? null : typeInfo.BaseType;
					packet.WriteTypeRef(baseTypeInfo == null ? null : new TypeRef(baseTypeInfo));
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
				default:
					return false;
			}
			return true;
		}
	}
}