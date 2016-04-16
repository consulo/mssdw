using DotNetty.Transport.Channels;
using DotNetty.Buffers;
using System.Text;
using Consulo.Internal.Mssdw.Server.Event;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace Consulo.Internal.Mssdw.Server
{
	public class NettyClient
	{
		private IChannel myChannel;
		private DebuggerNettyHandler myHandler;

		public NettyClient(IChannel channel, DebuggerNettyHandler handler)
		{
			myChannel = channel;
			myHandler = handler;
		}

		public Task<ClientMessage> Notify<T>(T e) where T : class
		{
			return Task.Run<ClientMessage>(async () =>
			{
				OnEvent<T> onEvent = new OnEvent<T>(e);

				onEvent.Id = Guid.NewGuid().ToString();

				byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(onEvent));

				IByteBuffer buffer = Unpooled.Buffer(messageBytes.Length);

				buffer.WriteBytes(messageBytes);

				Semaphore semaphore = new  Semaphore(0, 1);
				ClientMessage clientAnswer = null;
				myHandler.PutWaiter(onEvent.Id, obj =>
				{
					clientAnswer = obj;
					semaphore.Release();
				});

				await myChannel.WriteAndFlushAsync(buffer);

				semaphore.WaitOne();
				return clientAnswer;
			});
		}
	}
}