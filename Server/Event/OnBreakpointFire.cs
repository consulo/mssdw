namespace Consulo.Internal.Mssdw.Server.Event
{
	public class OnBreakpointFire
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

		//public int Column = -1;

		public OnBreakpointFire(string fileName, int line)
		{
			FilePath = fileName;
			Line = line;
		}
	}
}