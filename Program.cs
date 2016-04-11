using System;
using System.Linq;
using System.Threading;
using Consulo.Internal.Mssdw;
using Consulo.Internal.Mssdw.Request;
using Microsoft.Samples.Debugging.CorDebug;
using System.Collections.Generic;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using System.Reflection;
using Microsoft.Samples.Debugging.CorMetadata;
using System.Diagnostics.SymbolStore;
using Consulo.Internal.Mssdw.Data;

public class Program
{
	public static void Main(String[] args)
	{
		DebugSession session = new DebugSession();
		session.OnCodeFileLoad += delegate(DebugSession arg1, string fileName)
		{
			Console.WriteLine("code file loaded: " + fileName);

			BreakpointRequestResult result = session.InsertBreakpoint(new BreakpointRequest(fileName, 6));

			Console.WriteLine(result);
		};

		session.OnStop += delegate(DebugSession obj)
		{
			List<CorFrame> frames = obj.FrameList;

			foreach (CorFrame f in frames)
			{
				StackFrame frame = CreateFrame(obj, f);

				Console.WriteLine(frame.Location.Method + ":" + frame.Language);
			}
		};

		session.Start(args);

		while(!session.Finished)
		{
			Thread.Sleep(500);
		}
	}

	internal static StackFrame CreateFrame (DebugSession session, CorFrame frame)
	{
		// TODO: Fix remaining.
		uint address = 0;
		//string typeFQN;
		//string typeFullName;
		string addressSpace = "";
		string file = "";
		int line = 0;
		int endLine = 0;
		int column = 0;
		int endColumn = 0;
		string method = "";
		string lang = "";
		string module = "";
		string type = "";
		bool hasDebugInfo = false;
		bool hidden = false;
		bool external = true;

		if(frame.FrameType == CorFrameType.ILFrame)
		{
			if(frame.Function != null)
			{
				module = frame.Function.Module.Name;
				CorMetadataImport importer = new CorMetadataImport(frame.Function.Module);
				MethodInfo mi = importer.GetMethodInfo(frame.Function.Token);
				method = mi.DeclaringType.FullName + "." + mi.Name;
				type = mi.DeclaringType.FullName;
				addressSpace = mi.Name;

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

				if(session.IsExternalCode(file))
				{
					external = true;
				}
				else
				{
					/*if (session.Options.ProjectAssembliesOnly) {
						external = mi.GetCustomAttributes(true).Any(v =>
						v is System.Diagnostics.DebuggerHiddenAttribute ||
						v is System.Diagnostics.DebuggerNonUserCodeAttribute);
					} else */
					{
						external = mi.GetCustomAttributes(true).Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
					}
				}
				hidden = mi.GetCustomAttributes(true).Any(v => v is System.Diagnostics.DebuggerHiddenAttribute);
			}
			lang = "Managed";
			hasDebugInfo = true;
		}
		else if(frame.FrameType == CorFrameType.NativeFrame)
		{
			frame.GetNativeIP(out address);
			method = "<Unknown>";
			lang = "Native";
		}
		else if(frame.FrameType == CorFrameType.InternalFrame)
		{
			switch(frame.InternalFrameType)
			{
				case CorDebugInternalFrameType.STUBFRAME_M2U:
					method = "[Managed to Native Transition]";
					break;
				case CorDebugInternalFrameType.STUBFRAME_U2M:
					method = "[Native to Managed Transition]";
					break;
				case CorDebugInternalFrameType.STUBFRAME_LIGHTWEIGHT_FUNCTION:
					method = "[Lightweight Method Call]";
					break;
				case CorDebugInternalFrameType.STUBFRAME_APPDOMAIN_TRANSITION:
					method = "[Application Domain Transition]";
					break;
				case CorDebugInternalFrameType.STUBFRAME_FUNC_EVAL:
					method = "[Function Evaluation]";
					break;
			}
		}

		if(method == null)
			method = "<Unknown>";

		var loc = new SourceLocation(method, file, line, column, endLine, endColumn);
		return new StackFrame((long)address, addressSpace, loc, lang, external, hasDebugInfo, hidden);
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
