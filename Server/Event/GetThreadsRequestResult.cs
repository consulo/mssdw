using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetThreadsRequestResult
	{
		public class ThreadInfo
		{
			public int Id;
		}

		public List<ThreadInfo> Threads = new List<ThreadInfo>();

		public void Add(int id)
		{
			ThreadInfo threadInfo = new ThreadInfo();
			threadInfo.Id = id;
			Threads.Add(threadInfo);
		}
	}
}