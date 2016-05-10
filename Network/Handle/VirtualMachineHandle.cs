using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.CorSymbolStore;

namespace Consulo.Internal.Mssdw.Network.Handle
{
	internal class VirtualMachineHandle
	{
		internal class DebugInformationResult
		{
			internal string myModuleName;
			internal int myMethodToken;
			internal int myOffset;

			public DebugInformationResult(string moduleName, int methodId, int token)
			{
				myModuleName = moduleName;
				myMethodToken = methodId;
				myOffset = token;
			}
		}

		internal const int Version = 1;
		internal const int AllThreads = 2;
		internal const int Suspend = 3;
		internal const int Resume = 4;
		internal const int Exit = 5;
		internal const int Dispose = 6;
		internal const int InvokeMethod = 7;
		internal const int FindType = 10;
		internal const int FindDebugOffset = 11;

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
					debugSession.Process.Terminate(0);
					break;
				case Dispose:
					debugSession.Stopping = true;
					try
					{
						debugSession.Process.Continue(false);
					}
					catch
					{
					}
					debugSession.Process.Detach();
					break;
				case FindType:
					string qName = packet.ReadString();
					TypeRef findTypeByName = debugSession.FindTypeByName(qName);
					packet.WriteTypeRef(findTypeByName);
					break;
				case FindDebugOffset:
					string path = packet.ReadString();
					int line = packet.ReadInt();
					int column = packet.ReadInt();
					DebugInformationResult result = DoFindDebugOffset(debugSession, path, line, column);
					packet.WriteBool(result != null);
					if(result != null)
					{
						packet.WriteString(result.myModuleName);
						packet.WriteInt(result.myMethodToken);
						packet.WriteInt(result.myOffset);
					}
					break;
				case InvokeMethod:
				{
					int threadId = packet.ReadInt();
					int stackFrameId = packet.ReadInt();
					TypeRef typeRef = packet.ReadTypeRef();
					int methodId = packet.ReadInt();
					int argumentsSize = packet.ReadInt();
					CorValue[] arguments = new CorValue[argumentsSize];
					for(int i = 0; i < argumentsSize; i++)
					{
						arguments[i] = packet.ReadValue();
					}

					CorThread corThread = debugSession.GetThread(threadId);

					CorMetadataImport module = debugSession.GetMetadataForModule(typeRef.ModuleName);

					CorFunction corFunction = module.Module.GetFunctionFromToken(methodId);

					CorValue evalResult = debugSession.Evaluate(corThread, eval =>
					{
						eval.CallFunction(corFunction, arguments);
					});

					packet.WriteBool(true);  // all is ok
					packet.WriteValue(evalResult, debugSession);
					break;
				}
				default:
					return false;
			}
			return true;
		}

		public static DebugInformationResult DoFindDebugOffset(DebugSession debugSession, string locationPath, int locationLine, int locationColumn)
		{
			DebugSession.DocInfo doc;
			if(!debugSession.documents.TryGetValue(System.IO.Path.GetFullPath(locationPath), out doc))
			{
				return null;
			}

			int line;
			try
			{
				line = doc.Document.FindClosestLine(locationLine);
			}
			catch
			{
				return null;
			}

			ISymbolMethod met = null;
			if(doc.Reader is ISymbolReader2)
			{
				ISymbolMethod[] methods = ((ISymbolReader2)doc.Reader).GetMethodsFromDocumentPosition(doc.Document, line, 0);
				if(methods != null && methods.Any())
				{
					if(methods.Count() == 1)
					{
						met = methods[0];
					}
					else
					{
						int deepest = -1;
						foreach (ISymbolMethod method in methods)
						{
							var firstSequence = method.GetSequencePoints().FirstOrDefault((sp) => sp.StartLine != 0xfeefee);
							if(firstSequence != null && firstSequence.StartLine >= deepest)
							{
								deepest = firstSequence.StartLine;
								met = method;
							}
						}
					}
				}
			}
			if(met == null)
			{
				met = doc.Reader.GetMethodFromDocumentPosition(doc.Document, line, 0);
			}
			if(met == null)
			{
				return null;
			}

			int offset = -1;
			int firstSpInLine = -1;
			foreach (SequencePoint sp in met.GetSequencePoints())
			{
				if(sp.IsInside(doc.Document.URL, line, locationColumn))
				{
					offset = sp.Offset;
					break;
				}
				else if(firstSpInLine == -1 && sp.StartLine == line && sp.Document.URL.Equals(doc.Document.URL, StringComparison.OrdinalIgnoreCase))
				{
					firstSpInLine = sp.Offset;
				}
			}

			if(offset == -1)
			{
				//No exact match? Use first match in that line
				offset = firstSpInLine;
			}

			if(offset == -1)
			{
				return null;
			}

			return new DebugInformationResult(doc.Module.Name, met.Token.GetToken(), offset);
		}
	}
}