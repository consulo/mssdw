using System;
using Consulo.Internal.Mssdw.Server.Request;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public enum BreakEventStatus
	{
		Bound,
		Invalid,
		NotBound,
		NoDoc,
		NoMethod
	}

	public class InsertBreakpointRequestResult
	{
		public BreakEventStatus Status
		{
			get;
			set;
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