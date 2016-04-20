using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class CharValueResult
	{
		public int Id;

		public char Value;

		public CharValueResult(CorValue original, CorGenericValue genericValue)
		{
			Id = original == null ? -1 : original.Id;

			Value = (char) genericValue.GetValue();
		}
	}
}