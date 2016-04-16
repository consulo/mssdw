namespace Consulo.Internal.Mssdw.Server.Event.Request
{
	public class InsertBreakpointRequest
	{
		public string FilePath
		{
			get;
			set;
		}

		public int Line
		{
			get;
			set;
		}

		public int Column = -1;

		public InsertBreakpointRequest(string fileName, int line)
		{
			FilePath = fileName;
			Line = line;
		}
	}
}