using System.Collections.Generic;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class VirtualMachineHandle
	{
		internal const int Version = 1;
		internal const int AllThreads = 2;
		internal const int Suspend = 3;
		internal const int Resume = 4;
		internal const int Exit = 5;
		internal const int Dispose = 6;
		internal const int FindType = 10;

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			switch(packet.Command)
			{
				case Version:
					packet.WriteString("mssdw");
					packet.WriteInt(1);
					packet.WriteInt(0);
					break;
				case AllThreads:
					IEnumerable<CorThread> threads = debugSession.Process.Threads;
					List<CorThread> list = new List<CorThread>(threads);
					packet.WriteInt(list.Count);
					foreach (CorThread corThread in list)
					{
						packet.WriteInt(corThread.Id);
					}
					break;
				case Suspend:
					debugSession.Process.Stop(-1);
					break;
				case Resume:
					debugSession.Process.Continue(false);
					break;
				case Exit:
				case Dispose:
					debugSession.Process.Terminate(0);
					break;
				case FindType:
					string qName = packet.ReadString();
					TypeRef findTypeByName = debugSession.FindTypeByName(qName);
					packet.WriteTypeRef(findTypeByName);
					break;
				default:
					return false;
			}
			return true;
		}
	}
}