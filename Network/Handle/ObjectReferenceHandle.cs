using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class ObjectReferenceHandle
	{
		internal const int GetValue = 1;

		internal static bool Handle(Packet packet, DebugSession debugSession)
		{
			int objectId = packet.ReadInt();
			int fieldId = packet.ReadInt();

			switch(packet.Command)
			{
				case GetValue:
				{
					CorObjectValue objectValue = CorValueRegistrator.Get(objectId) as CorObjectValue;
					CorClass corClass = objectValue.Class;
					packet.WriteValue(objectValue.GetFieldValue(corClass, fieldId), debugSession);
					break;
				}
				default:
					return false;
			}

			return true;
		}
	}
}