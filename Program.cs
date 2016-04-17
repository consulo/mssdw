using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Consulo.Internal.Mssdw;
using Consulo.Internal.Mssdw.Server;

public class Program
{
	public static void Main(string[] temp)
	{
		List<string> arguments = new List<string>(temp);

		int port = -1;
		foreach (string argument in temp)
		{
			if(argument.StartsWith("--port="))
			{
				port = int.Parse(argument.Substring(7, argument.Length - 7));
				arguments.Remove(argument);
			}
		}

		if(port == -1)
		{
			Console.WriteLine("Port is not set. use '--port=<port>' argument");
			return;
		}

		Console.WriteLine("Port: " + port);

		DebugSession session = new DebugSession();

		try
		{
			NettyServer server = new NettyServer(port);

			Task.Run(() => server.RunServer(session)).Wait();

			Console.WriteLine("Waiting client");
			// w8 client
			while(session.Client == null)
			{
				Thread.Sleep(500);
			}
			Console.WriteLine("Client connected");

			session.Start(arguments); // we can failed if file is not exists

			/*session.OnStop += delegate(DebugSession obj)
			{
				List<CorFrame> frames = obj.FrameList;

				foreach (CorFrame f in frames)
				{
					StackFrame frame = CreateFrame(obj, f);

					Console.WriteLine(frame.Location.Method + ":" + frame.Language);
				}
			};     */

			Semaphore semaphore = new  Semaphore(0, 1);

			session.OnProcessExit += delegate(DebugSession obj)
			{
				server.Close();
				semaphore.Release();
			};

			semaphore.WaitOne();
		}
		catch(Exception)
		{
			throw;
		}
	}
}
