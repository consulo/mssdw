using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class StackFrameHandle
	{
		private const int GetLocalValue = 1;
		private const int GetThis = 2;
		private const int GetArgumentValue = 3;

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
				{
					int localVariableIndex = packet.ReadInt();
					CorValue value = corFrame == null ? null : corFrame.GetLocalVariable(localVariableIndex);
					packet.WriteValue(value, debugSession);
					break;
				}
				case GetArgumentValue:
				{
					if(corFrame == null)
					{
						packet.WriteValue(null, debugSession);
					}
					else
					{
						CorMetadataImport module = debugSession.GetMetadataForModule(corFrame.Function.Module.Name);
						MetadataMethodInfo methodInfo = module.GetMethodInfo(corFrame.FunctionToken);

						int parameterIndex = packet.ReadInt();
						if((methodInfo.Attributes & MethodAttributes.Static) == 0)
						{
							parameterIndex ++; // skip this
						}

						CorValue value = corFrame.GetArgument(parameterIndex);
						packet.WriteValue(value, debugSession);
					}
					break;
				}
				case GetThis:
					if(corFrame == null)
					{
						packet.WriteValue(null, debugSession);
					}
					else
					{
						CorMetadataImport module = debugSession.GetMetadataForModule(corFrame.Function.Module.Name);
						MetadataMethodInfo methodInfo = module.GetMethodInfo(corFrame.FunctionToken);
						packet.WriteValue((methodInfo.Attributes & MethodAttributes.Static) != 0 ? null : corFrame.GetArgument(0), debugSession);
					}
					break;
				default:
					return false;
			}
			return true;
		}
	}
}