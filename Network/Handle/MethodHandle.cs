using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class MethodHandle
	{
		private const int GetName = 1;
		private const int GetInfo = 6;

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
				case GetInfo:
					packet.WriteInt(methodInfo == null ? 0 : (int)methodInfo.Attributes);
					packet.WriteInt(methodInfo == null ? 0 : (int)methodInfo.ImplAttributes);
					break;
				default:
					return false;
			}

			return true;
		}
	}
}