using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class StringValueResult
	{
		public int Id;
		public string Value;

		public StringValueResult(int id, CorStringValue value)
		{
			Id = id;
			Value = value.String;
		}
	}
}