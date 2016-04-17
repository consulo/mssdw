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
		private IChannel bootstrapChannel;
		private MultithreadEventLoopGroup bossGroup = new MultithreadEventLoopGroup(1);
		private MultithreadEventLoopGroup workerGroup = new MultithreadEventLoopGroup();
		private ObservableEventListener eventListener = new ObservableEventListener();

		private int port;

		public NettyServer(int port)
		{
			this.port = port;
		}

		public async void RunServer(DebugSession debugSession)
		{
			eventListener.LogToConsole();
			//eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Informational);

			var bootstrap = new ServerBootstrap();
			bootstrap
			.Group(bossGroup, workerGroup)
			.Channel<TcpServerSocketChannel>()
			.Option(ChannelOption.SoBacklog, 100)
			.Handler(new LoggingHandler(LogLevel.INFO))
			.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
			{
				IChannelPipeline pipeline = channel.Pipeline;

				pipeline.AddLast(new DebuggerNettyHandler(debugSession));
			}));

			bootstrapChannel = await bootstrap.BindAsync(port);
		}

		public async void Close()
		{
			if(bootstrapChannel == null)
			{
				return;
			}

			await bootstrapChannel.CloseAsync();

			Task.WaitAll(bossGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());

			eventListener.Dispose();
		}
	}
}