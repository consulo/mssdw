using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class ServerMessage<T> where T : class
	{
		public string Id;

		public string Type;

		public T Object;

		public ServerMessage(T o)
		{
			Type = o.GetType().Name;
			Object = o;
		}
	}
}