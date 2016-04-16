using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class ServerMessage<T> where T : class
	{
		public String Id;

		public String Type;

		public T Object;

		public ServerMessage(T o)
		{
			Type = o.GetType().Name;
			Object = o;
		}
	}
}