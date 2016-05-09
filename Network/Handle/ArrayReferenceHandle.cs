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
					CorArrayValue corArrayValue = FindCorArrayValue(CorValueRegistrator.Get(objectId));
					if(corArrayValue != null)
					{
						CorValue value = corArrayValue.GetElement(new int[]{index});
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

		private static CorArrayValue FindCorArrayValue(CorValue value)
		{
			CorReferenceValue toReferenceValue = value.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				if(toReferenceValue.IsNull)
				{
					return null;
				}

				return FindCorArrayValue(toReferenceValue.Dereference());
			}

			return value.CastToArrayValue();
		}
	}
}