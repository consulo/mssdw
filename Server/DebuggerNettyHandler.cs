using System;
using System.Text;
using DotNetty.Transport.Channels;
using DotNetty.Buffers;
using Newtonsoft.Json;
using Consulo.Internal.Mssdw.Server.Event;
using System.Collections.Generic;
using Consulo.Internal.Mssdw.Server.Event.Request;

namespace Consulo.Internal.Mssdw.Server
{
	public class DebuggerNettyHandler : ChannelHandlerAdapter
	{
		private NettyClient client;

		private DebugSession debugSession;

		private Dictionary<string, Action<ClientMessage>> queries = new Dictionary<string, Action<ClientMessage>>();

		public DebuggerNettyHandler(DebugSession debugSession)
		{
			this.debugSession = debugSession;
		}

		public override void ChannelRegistered(DotNetty.Transport.Channels.IChannelHandlerContext context)
		{
			debugSession.Client = client = new NettyClient(context.Channel, this);
		}

		public override void ChannelUnregistered(DotNetty.Transport.Channels.IChannelHandlerContext context)
		{
			debugSession.Client = null;

			// force paused threads
			foreach (KeyValuePair<string, Action<ClientMessage>> keyValue in queries)
			{
				ClientMessage clientAnswer = new ClientMessage();
				clientAnswer.Id = keyValue.Key;
				clientAnswer.Continue = true;

				keyValue.Value(clientAnswer);
			}
		}

		public override void ChannelRead(IChannelHandlerContext context, object message)
		{
			IByteBuffer buffer = message as IByteBuffer;
			if(buffer != null)
			{
				string jsonContext = buffer.ToString(Encoding.UTF8);

				try
				{
					ClientMessage clientMessage = JsonConvert.DeserializeObject<ClientMessage>(jsonContext, new ClientMessageConverter());

					Action<ClientMessage> action;
					if(!queries.TryGetValue(clientMessage.Id, out action))
					{
						object messageObject = clientMessage.Object;
						if(messageObject is InsertBreakpointRequest)
						{
							InsertBreakpointRequestResult result = debugSession.InsertBreakpoint((InsertBreakpointRequest)messageObject);

							ServerMessage<InsertBreakpointRequestResult> serverMessage = new ServerMessage<InsertBreakpointRequestResult>(result);
							serverMessage.Id = clientMessage.Id;
							client.Write(serverMessage);
						}
					}
					else
					{
						action(clientMessage);
					}
				}
				catch(Exception e)
				{
					Console.WriteLine("Erro with: " + jsonContext);
					Console.WriteLine(e.StackTrace);
				}
			}
		}

		public override void ChannelReadComplete(IChannelHandlerContext context)
		{
			context.Flush();
		}

		public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
		{
			Console.WriteLine("Exception: " + exception);
		}

		public void PutWaiter(string id, Action<ClientMessage> action)
		{
			queries.Add(id, action);
		}
	}
}