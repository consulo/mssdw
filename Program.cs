using System;
using System.Collections.Generic;
using Consulo.Internal.Mssdw;
using Consulo.Internal.Mssdw.Network;

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

		JdwpConnection conn = new JdwpConnection(port);
		#if DEBUG
		Console.WriteLine("Waiting client at port: " + port);
		#endif
		conn.Bind();

		DebugSession session = new DebugSession();
		session.Start(arguments);

		session.OnProcessExit += delegate(DebugSession obj)
		{
			conn.Close();
		};

		JdwpHandler handler = new JdwpHandler(conn);
		handler.Run();
	}
}
