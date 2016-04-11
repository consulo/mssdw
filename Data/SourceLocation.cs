using System;

namespace Consulo.Internal.Mssdw.Data
{
	public class SourceLocation
	{
		public String Method
		{
			get;
			set;
		}

		public SourceLocation(string method, string file, int line, int column, int endLine, int endColumn)
		{
			Method = method;
		}
	}
}