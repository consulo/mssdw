using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetFramesRequestResult
	{
		public class FrameInfo
		{
			public class SourcePosition
			{
				public string FilePath;

				public int Line;
			}

			public SourcePosition Position = new SourcePosition();

			public string Method;
		}

		public List<FrameInfo> Frames = new List<FrameInfo>();

		public void Add(string filePath, int line, string method)
		{
			FrameInfo frameInfo = new FrameInfo();
			frameInfo.Method = method;

			frameInfo.Position.Line = line;
			frameInfo.Position.FilePath = filePath;

			Frames.Add(frameInfo);
		}
	}
}