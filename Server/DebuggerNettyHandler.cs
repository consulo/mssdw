using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace Consulo.Internal.Mssdw.Server
{
	public class DebuggerNettyHandler : ChannelHandlerAdapter
	{
		public override void ChannelRead(IChannelHandlerContext context, object message)
		{
			Console.WriteLine("message: " + message.ToString());
			IByteBuffer buffer = message as IByteBuffer;
			if(buffer != null)
			{
				Console.WriteLine("Received from client: " + buffer.ToString(Encoding.UTF8));
			}
			context.WriteAsync(message);
		}

		public override void ChannelReadComplete(IChannelHandlerContext context)
		{
			context.Flush();
		}

		public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
		{
			Console.WriteLine("Exception: " + exception);
			context.CloseAsync();
		}
	}
}