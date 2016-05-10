//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Diagnostics;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;

namespace Microsoft.Samples.Debugging.CorDebug
{
	public abstract class CorBreakpoint : WrapperBase
	{
		[CLSCompliant(false)]
		protected CorBreakpoint(ICorDebugBreakpoint managedBreakpoint) : base(managedBreakpoint)
		{
			Debug.Assert(managedBreakpoint != null);
			m_corBreakpoint = managedBreakpoint;
		}

		public virtual bool Active
		{
			get
			{
				int r = 0;
				m_corBreakpoint.IsActive(out r);
				return !(r == 0);
			}
			set
			{
				m_corBreakpoint.Activate(value ? 1 : 0);
			}
		}

		private ICorDebugBreakpoint m_corBreakpoint;
	}
}
