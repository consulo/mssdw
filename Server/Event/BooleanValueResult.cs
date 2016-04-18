using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class BooleanValueResult
	{
		public int Id;
		public bool Value;

		public BooleanValueResult(int id, CorGenericValue value)
		{
			Id = id;
			Value = (bool)value.GetValue();
		}
	}
}