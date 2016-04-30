namespace Consulo.Internal.Mssdw.Server.Request
{
	public class GetFieldValueRequest
	{
		public int ThreadId;

		public int StackFrameIndex;

		public TypeRef Type;

		public int ObjectId;

		public int FieldToken;
	}
}