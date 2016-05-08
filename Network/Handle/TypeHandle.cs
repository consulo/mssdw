using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class TypeHandle
	{
		private const int GetInfo = 1;

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
				default:
					return false;
			}
			return true;
		}
	}
}