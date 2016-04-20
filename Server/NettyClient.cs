using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Consulo.Internal.Mssdw.Server.Event;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Newtonsoft.Json;

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
				ServerMessage<T> onEvent = new ServerMessage<T>(e);

				onEvent.Id = Guid.NewGuid().ToString();

				string serializeObject = JsonConvert.SerializeObject(onEvent);

				//Console.WriteLine("send: " + serializeObject);

				byte[] messageBytes = Encoding.UTF8.GetBytes(serializeObject);

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

		public Task Write<T>(ServerMessage<T> e) where T : class
		{
			return Task.Run(async () =>
			{
				string serializeObject = JsonConvert.SerializeObject(e);

				byte[] messageBytes = Encoding.UTF8.GetBytes(serializeObject);

				IByteBuffer buffer = Unpooled.Buffer(messageBytes.Length);

				buffer.WriteBytes(messageBytes);
				await myChannel.WriteAndFlushAsync(buffer);

				//Console.WriteLine("send1: " + serializeObject);
			});
		}
	}
}