/*
  Copyright (C) 2009 Volker Berlin (vberlin@inetsoftware.de)

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.

  Jeroen Frijters
  jeroen@frijters.net

*/

using System;

namespace Consulo.Internal.Mssdw.Network
{
	class JdwpHandler
	{
		private readonly JdwpConnection conn;

		internal JdwpHandler(JdwpConnection conn)
		{
			this.conn = conn;
		}

		internal void Run()
		{
			while(true)
			{
				Packet packet = conn.ReadPacket();
				Console.Error.WriteLine("Packet:" + packet.CommandSet + " " + packet.Command);
				switch(packet.CommandSet)
				{
					case CommandSet.VirtualMachine:
						CommandSetVirtualMachine(packet);
						break;
					case CommandSet.EventRequest:
						CommandSetEventRequest(packet);
						break;
					default:
						NotImplementedPacket(packet);
						break;
				}
				conn.SendPacket(packet);
			}
		}

		/// <summary>
		/// http://java.sun.com/javase/6/docs/platform/jpda/jdwp/jdwp-protocol.html#JDWP_VirtualMachine
		/// </summary>
		/// <param name="packet"></param>
		private void CommandSetVirtualMachine(Packet packet)
		{
			switch(packet.Command)
			{
				case VirtualMachine.Version:
					packet.WriteString("mssdw");
					packet.WriteInt(1);
					packet.WriteInt(0);
					break;

				default:
					NotImplementedPacket(packet); // include a SendPacket
					break;
			}
		}


		/// <summary>
		/// http://java.sun.com/javase/6/docs/platform/jpda/jdwp/jdwp-protocol.html#JDWP_EventRequest
		/// </summary>
		/// <param name="packet"></param>
		private void CommandSetEventRequest(Packet packet)
		{
			switch(packet.Command)
			{
				case EventRequest.CmdSet:
					EventRequest eventRequest = EventRequest.create(packet);
					Console.Error.WriteLine(eventRequest);
					if(eventRequest == null)
					{
						NotImplementedPacket(packet);
					}
					else
					{
						//FIXME target.AddEventRequest(eventRequest);
						packet.WriteInt(eventRequest.RequestId);
					}
					break;
				default:
					NotImplementedPacket(packet);
					break;
			}
		}

		private void NotImplementedPacket(Packet packet)
		{
			Console.Error.WriteLine("================================");
			Console.Error.WriteLine("Not Implemented Packet:" + packet.CommandSet + "-" + packet.Command);
			Console.Error.WriteLine("================================");
			packet.Error = (short)Error.NOT_IMPLEMENTED;
		}
	}
}