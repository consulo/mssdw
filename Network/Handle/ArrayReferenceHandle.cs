using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class ArrayReferenceHandle
	{
		internal const int GetValue = 1;

		internal static bool Handle(Packet packet, DebugSession debugSession)
		{
			int objectId = packet.ReadInt();

			switch(packet.Command)
			{
				case GetValue:
				{
					int index = packet.ReadInt();
					CorArrayValue cor = CorValueRegistrator.Get(objectId) as CorArrayValue;
					if(cor != null)
					{
						CorValue value = cor.GetElement(new int[]{index});
						packet.WriteValue(value, debugSession);
					}
					else
					{
						packet.WriteValue(null, debugSession);
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