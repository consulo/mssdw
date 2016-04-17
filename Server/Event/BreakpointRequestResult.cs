using System;
using Consulo.Internal.Mssdw.Server.Request;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public enum BreakEventStatus
	{
		Bound,
		Invalid,
		NotBound
	}

	public class InsertBreakpointRequestResult
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

		public InsertBreakpointRequestResult(InsertBreakpointRequest breakpointRequest)
		{
			Request = breakpointRequest;
		}

		public void SetStatus(BreakEventStatus status, object o)
		{
			Status = status;
		}


		public void IncrementHitCount()
		{

		}

		public override string ToString()
		{
			return "BreakpointRequestResult: " + Enum.GetName(typeof(BreakEventStatus), Status);
		}
	}
}