using System;

namespace Consulo.Internal.Mssdw.Request {
    public class BreakpointRequest {
        public String FileName { get; set; }

        public int Line { get; set; }

        public int Column = -1;

        public bool Enabled = true;

        public BreakpointRequest(String fileName, int line) {
            FileName = fileName;
            Line = line;
        }
    }
}