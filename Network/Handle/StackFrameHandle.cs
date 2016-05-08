using System.Collections.Generic;
using System.Linq;
using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class StackFrameHandle
	{
		private const int GetLocalValue = 1;

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			int threadId = packet.ReadInt();
			int stackFrameId = packet.ReadInt();

			CorFrame corFrame = null;
			CorThread current = debugSession.Process.Threads.Where(x => x.Id == threadId).FirstOrDefault();
			if(current != null)
			{
				IEnumerable<CorFrame> frames = DebugSession.GetFrames(current);
				int i = 0;
				foreach (CorFrame frame in frames)
				{
					if(i == stackFrameId)
					{
						corFrame = frame;
						break;
					}
				}
			}

			switch(packet.Command)
			{
				case GetLocalValue:
					int localVariableIndex = packet.ReadInt();
					CorValue value = corFrame == null ? null : corFrame.GetLocalVariable(localVariableIndex);
					packet.WriteValue(value);
					break;
				default:
					return false;
			}
			return true;
		}
	}
}