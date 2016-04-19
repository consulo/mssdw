using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class BooleanValueResult
	{
		public int Id;
		public long Address;
		public bool Value;

		public BooleanValueResult(CorValue original, CorGenericValue value)
		{
			Id = original == null ? -1 : original.Id;
			Address = original == null ? -1 : original.Address;

			Value = (bool)value.GetValue();
		}
	}
}