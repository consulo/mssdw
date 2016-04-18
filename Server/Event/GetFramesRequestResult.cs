using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetFramesRequestResult
	{
		public class FrameInfo
		{
			public class SourcePosition
			{
				public string FilePath;

				public int Line;

				public int Column;
			}

			public SourcePosition Position = new SourcePosition();

			public TypeRef Type;

			public int FunctionToken;
		}

		public List<FrameInfo> Frames = new List<FrameInfo>();

		public void Add(string filePath, int line, int column, TypeRef typeRef, int functionToken)
		{
			FrameInfo frameInfo = new FrameInfo();
			frameInfo.Type = typeRef;
			frameInfo.FunctionToken = functionToken;

			frameInfo.Position.Line = line;
			frameInfo.Position.Column = column;
			frameInfo.Position.FilePath = filePath;

			Frames.Add(frameInfo);
		}
	}
}