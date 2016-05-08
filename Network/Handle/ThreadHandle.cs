using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class ThreadHandle
	{
		private const int GetFrameInfo = 1;
		private const int Name = 2;
		private const int GetState = 3;

		internal class GetFramesRequestResult
		{
			public class FrameInfo
			{
				public string FilePath;

				public int Line;

				public int Column;

				public TypeRef Type;

				public int FunctionToken;
			}

			public List<FrameInfo> Frames = new List<FrameInfo>();

			public void Add(string filePath, int line, int column, TypeRef typeRef, int functionToken)
			{
				FrameInfo frameInfo = new FrameInfo();
				frameInfo.Type = typeRef;
				frameInfo.FunctionToken = functionToken;

				frameInfo.Line = line;
				frameInfo.Column = column;
				frameInfo.FilePath = filePath;

				Frames.Add(frameInfo);
			}
		}

		public static bool Handle(Packet packet, DebugSession debugSession)
		{
			int threadId = packet.ReadInt();

			switch(packet.Command)
			{
				case GetFrameInfo:
				{
					GetFramesRequestResult result = new GetFramesRequestResult();
					CorThread corThread = debugSession.Process.Threads.Where(x => x.Id == threadId).FirstOrDefault();
					if(corThread != null)
					{
						foreach (CorFrame corFrame in DebugSession.GetFrames(corThread))
						{
							AddFrame(debugSession, corFrame, result);
						}
					}

					List<GetFramesRequestResult.FrameInfo> frames = result.Frames;
					packet.WriteInt(frames.Count);
					int i = 0;
					foreach (GetFramesRequestResult.FrameInfo frame in frames)
					{
						packet.WriteInt(i++);
						packet.WriteString(frame.FilePath);
						packet.WriteInt(frame.Line);
						packet.WriteInt(frame.Column);
						packet.WriteTypeRef(frame.Type);
						packet.WriteInt(frame.FunctionToken);
					}
					break;
				}
				case Name:
				{
					string threadName = "<invalid>";
					CorThread corThread = debugSession.Process.Threads.Where(x => x.Id == threadId).FirstOrDefault();
					if(corThread != null)
					{
						threadName = debugSession.GetThreadName(corThread);
					}

					packet.WriteString(threadName);
					break;
				}
				case GetState:
				{
					int state = 0;
					int userState = 0;
					CorThread corThread = debugSession.Process.Threads.Where(x => x.Id == threadId).FirstOrDefault();
					if(corThread != null)
					{
						state = (int) corThread.DebugState;
						userState = (int) corThread.UserState;
					}
					packet.WriteInt(state);
					packet.WriteInt(userState);
					break;
				}
				default:
					return false;
			}

			return true;
		}

		private static void AddFrame(DebugSession session, CorFrame frame, GetFramesRequestResult result)
		{
			uint address = 0;
			//string typeFQN;
			//string typeFullName;
			//string addressSpace = "";
			string file = "";
			int line = 0;
			int endLine = 0;
			int column = 0;
			int endColumn = 0;
			//string method = "";
			//string lang = "";
			//string module = "";
			//string type = "";
			bool hasDebugInfo = false;
			//bool hidden = false;
			//bool external = true;

			string moduleName = null;
			int classToken = -1;
			int functionToken = -1;

			if(frame.FrameType == CorFrameType.ILFrame)
			{
				if(frame.Function != null)
				{
					moduleName = frame.Function.Module.Name;
					classToken = frame.Function.Class.Token;
					functionToken = frame.FunctionToken;

					//module = frame.Function.Module.Name;
					//CorMetadataImport importer = new CorMetadataImport(frame.Function.Module);
					//MethodInfo mi = importer.GetMethodInfo(frame.Function.Token);
					//method = mi.DeclaringType.FullName + "." + mi.Name;
					//type = mi.DeclaringType.FullName;
					//addressSpace = mi.Name;

					var sp = GetSequencePoint(session, frame);
					if(sp != null)
					{
						line = sp.StartLine;
						column = sp.StartColumn;
						endLine = sp.EndLine;
						endColumn = sp.EndColumn;
						file = sp.Document.URL;
						address = (uint)sp.Offset;
					}

					///object[] customAttributes = mi.GetCustomAttributes(true);

					//if(session.IsExternalCode(file))
					//{
					//	external = true;
					//}
					//else
					//{
					/*if (session.Options.ProjectAssembliesOnly) {
						external = mi.GetCustomAttributes(true).Any(v =>
						v is System.Diagnostics.DebuggerHiddenAttribute ||
						v is System.Diagnostics.DebuggerNonUserCodeAttribute);
					} else */
					//{
					//	external = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
					//}
					//}
					//hidden = false;// customAttributes.Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
				}
				hasDebugInfo = true;
			}
			else if(frame.FrameType == CorFrameType.NativeFrame)
			{
				frame.GetNativeIP(out address);
			}
			else if(frame.FrameType == CorFrameType.InternalFrame)
			{
			}

			result.Add(file, line, column, new TypeRef(moduleName, classToken), functionToken);
		}

		private const int SpecialSequencePoint = 0xfeefee;

		public static SequencePoint GetSequencePoint(DebugSession session, CorFrame frame)
		{
			ISymbolReader reader = session.GetReaderForModule(frame.Function.Module.Name);
			if(reader == null)
				return null;

			ISymbolMethod met = reader.GetMethod(new SymbolToken(frame.Function.Token));
			if(met == null)
				return null;

			int SequenceCount = met.SequencePointCount;
			if(SequenceCount <= 0)
				return null;

			CorDebugMappingResult mappingResult;
			uint ip;
			frame.GetIP(out ip, out mappingResult);
			if(mappingResult == CorDebugMappingResult.MAPPING_NO_INFO || mappingResult == CorDebugMappingResult.MAPPING_UNMAPPED_ADDRESS)
				return null;

			int[] offsets = new int[SequenceCount];
			int[] lines = new int[SequenceCount];
			int[] endLines = new int[SequenceCount];
			int[] columns = new int[SequenceCount];
			int[] endColumns = new int[SequenceCount];
			ISymbolDocument[] docs = new ISymbolDocument[SequenceCount];
			met.GetSequencePoints(offsets, docs, lines, columns, endLines, endColumns);

			if((SequenceCount > 0) && (offsets[0] <= ip))
			{
				int i;
				for(i = 0; i < SequenceCount; ++i)
				{
					if(offsets[i] >= ip)
					{
						break;
					}
				}

				if((i == SequenceCount) || (offsets[i] != ip))
				{
					--i;
				}

				if(lines[i] == SpecialSequencePoint)
				{
					int j = i;
					// let's try to find a sequence point that is not special somewhere earlier in the code
					// stream.
					while(j > 0)
					{
						--j;
						if(lines[j] != SpecialSequencePoint)
						{
							return new SequencePoint()
							{
								IsSpecial = true,
								Offset = offsets[j],
								StartLine = lines[j],
								EndLine = endLines[j],
								StartColumn = columns[j],
								EndColumn = endColumns[j],
								Document = docs[j]
							};
						}
					}
					// we didn't find any non-special seqeunce point before current one, let's try to search
					// after.
					j = i;
					while(++j < SequenceCount)
					{
						if(lines[j] != SpecialSequencePoint)
						{
							return new SequencePoint()
							{
								IsSpecial = true,
								Offset = offsets[j],
								StartLine = lines[j],
								EndLine = endLines[j],
								StartColumn = columns[j],
								EndColumn = endColumns[j],
								Document = docs[j]
							};
						}
					}

					// Even if sp is null at this point, it's a valid scenario to have only special sequence
					// point in a function.  For example, we can have a compiler-generated default ctor which
					// doesn't have any source.
					return null;
				}
				else
				{
					return new SequencePoint()
					{
						IsSpecial = false,
						Offset = offsets[i],
						StartLine = lines[i],
						EndLine = endLines[i],
						StartColumn = columns[i],
						EndColumn = endColumns[i],
						Document = docs[i]
					};
				}
			}
			return null;
		}
	}
}