using System;

namespace Consulo.Internal.Mssdw.Server.Event.Request
{
	public enum BreakEventStatus
	{
		Bound,
		Invalid,
		NotBound
	}

	public class BreakpointRequestResult
	{
		public BreakEventStatus Status
		{
			get;
			set;
		}

		public InsertBreakpointRequest Request
		{
			get;
			set;
		}

		public BreakpointRequestResult(InsertBreakpointRequest breakpointRequest)
		{
			Request = breakpointRequest;
		}

		public void SetStatus(BreakEventStatus status, Object o)
		{
			Status = status;
		}


		public void IncrementHitCount()
		{

		}

		public override String ToString()
		{
			return "BreakpointRequestResult: " + Enum.GetName(typeof(BreakEventStatus), Status);
		}
	}
}