using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetLocalsRequestResult
	{
		public class LocalInfo
		{
			public int Index;
			public string Name;
		}

		public List<LocalInfo> Locals = new List<LocalInfo>();

		public void Add(int index, string name)
		{
			LocalInfo localInfo = new LocalInfo();
			localInfo.Index = index;
			localInfo.Name = name;

			Locals.Add(localInfo);
		}
	}
}