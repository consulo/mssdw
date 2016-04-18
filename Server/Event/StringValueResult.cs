using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class StringValueResult
	{
		public int Id;
		public string Value;

		public StringValueResult(CorStringValue value)
		{
			Id = value.Id;
			Value = value.String;
		}
	}
}