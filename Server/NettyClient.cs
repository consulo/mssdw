using DotNetty.Transport.Channels;
using DotNetty.Buffers;
using System.Text;
using Consulo.Internal.Mssdw.Server.Event;
using Newtonsoft.Json;
using System;

namespace Consulo.Internal.Mssdw.Server
{
	public class NettyClient
	{
		private IChannel myChannel;

		public NettyClient(IChannel channel)
		{
			myChannel = channel;
		}

		public void Notify<T>(T e) where T : class
		{
			OnEvent<T> onEvent = new OnEvent<T>(e);

			onEvent.Id = Guid.NewGuid().ToString();

			byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(onEvent));

			IByteBuffer buffer = Unpooled.Buffer(messageBytes.Length);

			buffer.WriteBytes(messageBytes);

			myChannel.WriteAndFlushAsync(buffer);
		}
	}
}