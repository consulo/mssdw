using System;

namespace Consulo.Internal.Mssdw.Data
{
	public class StackFrame
	{
		public SourceLocation Location
		{
			get;
			set;
		}

		public string Language
		{
			get;
			set;
		}

		public StackFrame(long address, string addressSpace, SourceLocation sourceLocation, string lang, bool external, bool hasDebugInfo, bool hidden)
		{
			Location = sourceLocation;
			Language = lang;
		}
	}
}