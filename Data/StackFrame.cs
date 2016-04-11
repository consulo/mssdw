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

		public String Language
		{
			get;
			set;
		}

		public StackFrame(long address, String addressSpace, SourceLocation sourceLocation, String lang, bool external, bool hasDebugInfo, bool hidden)
		{
			Location = sourceLocation;
			Language = lang;
		}
	}
}