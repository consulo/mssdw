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
					CorValue corValue = CorValueRegistrator.Get(objectId);
					CorObjectValue corObjectValue = FindCorObjectValue(corValue);
					if(corObjectValue == null)
					{
						packet.WriteValue(null, debugSession);
					}
					else
					{
						CorClass corClass = corObjectValue.Class;
						packet.WriteValue(corObjectValue.GetFieldValue(corClass, fieldId), debugSession);
					}
					break;
				}
				default:
					return false;
			}

			return true;
		}

		private static CorObjectValue FindCorObjectValue(CorValue value)
		{
			CorReferenceValue toReferenceValue = value.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				if(toReferenceValue.IsNull)
				{
					return null;
				}

				return FindCorObjectValue(toReferenceValue.Dereference());
			}

			return value.CastToObjectValue();
		}
	}
}