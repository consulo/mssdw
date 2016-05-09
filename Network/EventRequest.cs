using System;
using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Network
{
	public class EventRequest
	{
		internal const int CmdSet = 1;
		internal const int CmdClear = 2;
		internal const int CmdClearAllBreakpoints = 3;

		private static int eventRequestCounter;

		private readonly byte eventKind;
		private readonly byte suspendPolicy;
		private readonly List<EventModifier> modifiers;
		private readonly int requestId;

		private EventRequest(byte eventKind, byte suspendPolicy, List<EventModifier> modifiers)
		{
			this.eventKind = eventKind;
			this.suspendPolicy = suspendPolicy;
			this.modifiers = modifiers;
			this.requestId = ++eventRequestCounter;
		}

		/// <summary>
		/// Create a new EventRequest with the data in the Packet
		/// </summary>
		/// <param name="packet">a data packet send from the debugger</param>
		/// <returns>a new packet or null if there are some unknown types.</returns>
		internal static EventRequest create(Packet packet)
		{
			byte eventKind = packet.ReadByte(); // class EventKind
			switch(eventKind)
			{
				case Consulo.Internal.Mssdw.Network.EventKind.VM_START:
				case Consulo.Internal.Mssdw.Network.EventKind.VM_DEATH:
				case Consulo.Internal.Mssdw.Network.EventKind.THREAD_START:
				case Consulo.Internal.Mssdw.Network.EventKind.THREAD_DEATH:
				case Consulo.Internal.Mssdw.Network.EventKind.METHOD_ENTRY:
				case Consulo.Internal.Mssdw.Network.EventKind.METHOD_EXIT:
				case Consulo.Internal.Mssdw.Network.EventKind.BREAKPOINT:
				case Consulo.Internal.Mssdw.Network.EventKind.STEP:
				case Consulo.Internal.Mssdw.Network.EventKind.EXCEPTION:
				case Consulo.Internal.Mssdw.Network.EventKind.KEEPALIVE:
				case Consulo.Internal.Mssdw.Network.EventKind.USER_BREAK:
				case Consulo.Internal.Mssdw.Network.EventKind.USER_LOG:
				case Consulo.Internal.Mssdw.Network.EventKind.MODULE_LOAD:
				case Consulo.Internal.Mssdw.Network.EventKind.MODULE_UNLOAD:
					break;
				default:
					return null; //Invalid or not supported EventKind
			}
			byte suspendPolicy = packet.ReadByte();
			int count = packet.ReadByte();
			Console.Error.WriteLine("Set:" + eventKind + "-" + suspendPolicy + "-" + count);
			List<EventModifier> modifiers = new List<EventModifier>();
			for(int i = 0; i < count; i++)
			{
				byte modKind = packet.ReadByte(); // class EventModifierKind
				Console.Error.WriteLine("EventModifierKind:" + modKind);
				EventModifier modifier;
				switch(modKind)
				{
					case EventModifierKind.BreakpointLocation:
						modifier = new BreakpointLocation(packet);
						break;
					default:
						return null; //Invalid or not supported EventModifierKind
				}
				modifiers.Add(modifier);
			}
			return new EventRequest(eventKind, suspendPolicy, modifiers);
		}

		internal Modifier FindModifier<Modifier>() where Modifier : EventModifier
		{
			foreach (EventModifier mod in modifiers)
			{
				if(mod is Modifier)
				{
					return (Modifier)mod;
				}
			}
			throw new Exception("We can't find modifier by type: " + typeof(Modifier));
		}

		internal List<EventModifier> Modifiers
		{
			get
			{
				return modifiers;
			}
		}

		internal int RequestId
		{
			get
			{
				return requestId;
			}
		}

		internal int EventKind
		{
			get
			{
				return eventKind;
			}
		}

		public override string ToString()
		{
			//for debugging
			string str = "EventRequest:" + eventKind + "," + suspendPolicy + "[";
			for(int i = 0; i < modifiers.Count; i++)
			{
				str += modifiers[i] + ",";
			}
			str += "]";
			return str;
		}
	}

	abstract class EventModifier
	{
	}

	class BreakpointLocation : EventModifier
	{
		public string ModulePath { get; set; }

		public int MethodToken { get; set; }

		public int Offset { get; set; }

		internal BreakpointLocation(Packet packet)
		{
			ModulePath = packet.ReadString();
			MethodToken = packet.ReadInt();
			Offset = packet.ReadInt();
		}

		public override string ToString()
		{
			// for debugging
			return "BreakpointLocation:" + ModulePath + ":" + MethodToken + ":" + Offset;
		}
	}
}