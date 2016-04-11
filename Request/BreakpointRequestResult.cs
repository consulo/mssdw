using System;

namespace Consulo.Internal.Mssdw.Request {
    public enum BreakEventStatus {
        Bound,
        Invalid,
        NotBound
    }

    public class BreakpointRequestResult {
        public BreakEventStatus Status { get; set; }

        public BreakpointRequest BreakEvent { get; set; }

        public BreakpointRequestResult(BreakpointRequest breakpointRequest) {
            BreakEvent = breakpointRequest;
        }

        public void SetStatus(BreakEventStatus status, Object o) {
            Status = status;
        }


        public void IncrementHitCount() {

        }

        public override String ToString() {
            return "BreakpointRequestResult: " + Enum.GetName(typeof(BreakEventStatus), Status);
        }
    }
}