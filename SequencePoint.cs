using System.Diagnostics.SymbolStore;
using System;

namespace Consulo.Internal.Mssdw {
    public class SequencePoint {
        public int StartLine;
        public int EndLine;
        public int StartColumn;
        public int EndColumn;
        public int Offset;
        public bool IsSpecial;
        public ISymbolDocument Document;

        public bool IsInside (string fileUrl, int line, int column) {
            if (!Document.URL.Equals(fileUrl, StringComparison.OrdinalIgnoreCase))
                return false;
            if (line < StartLine || (line == StartLine && column < StartColumn))
                return false;
            if (line > EndLine || (line == EndLine && column > EndColumn))
                return false;
            return true;
        }
    }
}