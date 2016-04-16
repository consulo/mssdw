using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class OnEvent<T> where T : class
	{
		public String Id;

		public String Type;

		public T Object;

		public OnEvent(T o)
		{
			Type = o.GetType().Name;
			Object = o;
		}
	}
}