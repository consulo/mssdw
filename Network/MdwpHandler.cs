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
using System.Collections.Generic;
using Consulo.Internal.Mssdw.Network.Handle;
using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Network
{
	class MdwpHandler
	{
		private readonly MdwpConnection conn;
		private readonly DebugSession myDebugSession;

		internal MdwpHandler(MdwpConnection conn, DebugSession debugSession)
		{
			this.conn = conn;
			myDebugSession = debugSession;
		}

		internal void Run()
		{
			while(myDebugSession.Process != null)
			{
				Packet packet = conn.ReadPacket();
				try
				{
					switch(packet.CommandSet)
					{
						case CommandSet.VirtualMachine:
							if(!VirtualMachineHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;
						case CommandSet.StackFrame:
							if(!StackFrameHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;
						case CommandSet.Method:
							if(!MethodHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;
						case CommandSet.Type:
							if(!TypeHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;
						case CommandSet.Thread:
							if(!ThreadHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;
						case CommandSet.ObjectReference:
							if(!ObjectReferenceHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}
							break;;
						case CommandSet.ArrayReference:
							if(!ArrayReferenceHandle.Handle(packet, myDebugSession))
							{
								NotImplementedPacket(packet);
							}

							break;;
						case CommandSet.EventRequest:
							CommandSetEventRequest(packet);
							break;
						default:
							NotImplementedPacket(packet);
							break;
					}
				}
				catch(Exception e)
				{
					Console.WriteLine(e.Message);
					Console.WriteLine(e.StackTrace);
				}
				conn.SendPacket(packet);
			}
		}

		private void CommandSetEventRequest(Packet packet)
		{
			switch(packet.Command)
			{
				case EventRequest.CmdSet:
					EventRequest eventRequest = EventRequest.create(packet);
					if(eventRequest == null)
					{
						NotImplementedPacket(packet);
					}
					else
					{
						myDebugSession.AddEventRequest(eventRequest);
						packet.WriteInt(eventRequest.RequestId);
					}
					break;
				case EventRequest.CmdClear:
					int requestId = packet.ReadInt();
					myDebugSession.RemoveEventRequest(requestId);
					break;
				case EventRequest.CmdClearAllBreakpoints:
					myDebugSession.RemoveBreakpointEventRequests();
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