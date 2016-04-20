using Microsoft.Samples.Debugging.CorDebug;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class NumberValueResult
	{
		public int Id;

		public int Type;

		public string Value;

		public NumberValueResult(CorValue original, CorElType corElType, CorGenericValue genericValue)
		{
			Id = original == null ? -1 : original.Id;
			Type = (int) corElType;

			object valueGetValue = genericValue.GetValue();

			Value = valueGetValue.ToString();
		}
	}
}