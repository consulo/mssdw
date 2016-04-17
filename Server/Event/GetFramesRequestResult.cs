using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetFramesRequestResult
	{
		public class FrameInfo
		{
			public string Method;
		}

		public List<FrameInfo> Frames = new List<FrameInfo>();

		public void Add(string method)
		{
			FrameInfo threadInfo = new FrameInfo();
			threadInfo.Method = method;

			Frames.Add(threadInfo);
		}
	}
}