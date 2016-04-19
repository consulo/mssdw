namespace Consulo.Internal.Mssdw.Server.Event
{
	public class FindTypeInfoRequestResult
	{
		public TypeRef Type;

		public FindTypeInfoRequestResult(TypeRef typeRef)
		{
			Type = typeRef;
		}
	}
}