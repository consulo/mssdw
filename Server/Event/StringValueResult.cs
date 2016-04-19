using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class StringValueResult
	{
		public int Id;
		public long Address;
		public string Value;

		public StringValueResult(CorValue original, CorStringValue value)
		{
			Id = original == null ? -1 : original.Id;
			Address = original == null ? -1 : original.Address;

			Value = value.String;
		}
	}
}