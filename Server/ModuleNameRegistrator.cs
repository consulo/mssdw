using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server
{
	public static class ModuleNameRegistrator
	{
		private static Dictionary<string, int> values = new Dictionary<string, int>();
		private static Dictionary<int, string> values2 = new Dictionary<int, string>();
		private static int nextId;

		public static int GetOrRegister(string value)
		{
			lock (values)
			{
				int index;
				if(!values.TryGetValue(value, out index))
				{
					values.Add(value, index = values.Count);
					values2.Add(index, value);
				}
				return index;
			}
		}

		public static string Get(int id)
		{
			lock (values)
			{
				return values2[id];
			}
		}
	}
}