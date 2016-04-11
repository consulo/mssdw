using System;
using System.Threading.Tasks;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

namespace Consulo.Internal.Mssdw.Server
{
	public class NettyServer
	{
		public NettyServer()
		{
		}

		public void Start()
		{
			Task.Run(() => RunServer()).Wait();
		}

		static async Task RunServer()
		{
			var eventListener = new ObservableEventListener();
			eventListener.LogToConsole();
			//eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

			var bossGroup = new MultithreadEventLoopGroup(1);
			var workerGroup = new MultithreadEventLoopGroup();
			try
			{
				var bootstrap = new ServerBootstrap();
				bootstrap
				.Group(bossGroup, workerGroup)
				.Channel<TcpServerSocketChannel>()
				.Option(ChannelOption.SoBacklog, 100)
				.Handler(new LoggingHandler(LogLevel.INFO))
				.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
				{
					IChannelPipeline pipeline = channel.Pipeline;

					pipeline.AddLast(new DebuggerNettyHandler());
				}));

				IChannel bootstrapChannel = await bootstrap.BindAsync(7755);

				Console.ReadLine();

				Console.WriteLine("close");
				await bootstrapChannel.CloseAsync();
				Console.WriteLine("after close");
			}
			finally
			{
				Task.WaitAll(bossGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());
				eventListener.Dispose();
			}
			Console.WriteLine("finish");
		}
	}
}