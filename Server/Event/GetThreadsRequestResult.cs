using System.Collections.Generic;
using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetThreadsRequestResult
	{
		public class ThreadInfo
		{
			public int Id;

			public string Name;
		}

		public List<ThreadInfo> Threads = new List<ThreadInfo>();

		public void Add(int id, string name)
		{
			ThreadInfo threadInfo = new ThreadInfo();
			threadInfo.Id = id;
			threadInfo.Name = name;
			Threads.Add(threadInfo);
		}
	}
}