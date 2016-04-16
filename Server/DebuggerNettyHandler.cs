using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace Consulo.Internal.Mssdw.Server
{
	public class DebuggerNettyHandler : ChannelHandlerAdapter
	{
		private DebugSession debugSession;

		public DebuggerNettyHandler(DebugSession debugSession)
		{
			this.debugSession = debugSession;
		}

		public override void ChannelRegistered(DotNetty.Transport.Channels.IChannelHandlerContext context)
		{
			debugSession.Client = new NettyClient(context.Channel);
		}

		public override void ChannelRead(IChannelHandlerContext context, object message)
		{
			/*IByteBuffer buffer = message as IByteBuffer;
			if(buffer != null)
			{
				string jsonContext = buffer.ToString(Encoding.UTF8);

				Console.WriteLine("wrote:" + jsonContext);
				Write(context, jsonContext);
			}  */
		}

		private void Write(IChannelHandlerContext c, String message)
		{
			/*byte[] messageBytes = Encoding.UTF8.GetBytes(message);
			IByteBuffer buffer = Unpooled.Buffer(messageBytes.Length);
			buffer.WriteBytes(messageBytes);

			c.WriteAndFlushAsync(buffer);    */
		}

		public override void ChannelReadComplete(IChannelHandlerContext context)
		{
			context.Flush();
		}

		public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
		{
			Console.WriteLine("Exception: " + exception);
		}
	}
}