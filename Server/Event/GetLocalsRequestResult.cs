using System.Collections.Generic;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetLocalsRequestResult
	{
		public class LocalInfo
		{
			public int Index;
			public string Name;
			public TypeRef Type;
		}

		public List<LocalInfo> Locals = new List<LocalInfo>();

		public void Add(int index, MetadataTypeInfo type, string name)
		{
			LocalInfo localInfo = new LocalInfo();
			localInfo.Index = index;
			localInfo.Name = name;
			localInfo.Type = new TypeRef(type);

			Locals.Add(localInfo);
		}
	}
}