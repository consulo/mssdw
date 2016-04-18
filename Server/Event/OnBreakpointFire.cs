namespace Consulo.Internal.Mssdw.Server.Event
{
	public class OnBreakpointFire
	{
		public int ActiveThreadId
		{
			get;
			set;
		}

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

		//public int Column = -1;

		public OnBreakpointFire(int activeThreadId, string fileName, int line)
		{
			ActiveThreadId = activeThreadId;
			FilePath = fileName;
			Line = line;
		}
	}
}