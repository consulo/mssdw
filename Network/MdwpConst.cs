/*
  Copyright (C) 2009 Volker Berlin (vberlin@inetsoftware.de)

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.

  Jeroen Frijters
  jeroen@frijters.net

*/

namespace Consulo.Internal.Mssdw.Network
{
	static class CommandSet
	{
		internal const int VirtualMachine = 1;
		internal const int Thread = 11;
		internal const int EventRequest = 15;
		internal const int Method = 22;
		internal const int Type = 23;
		internal const int Event = 64;
	}

	static class Error
	{
		internal const int NOT_IMPLEMENTED = 100;
	}

	static class EventKind
	{
		internal const int VM_START = 0;
		internal const int VM_DEATH = 1;
		internal const int THREAD_START = 2;
		internal const int THREAD_DEATH = 3;
		internal const int METHOD_ENTRY = 4;
		internal const int METHOD_EXIT = 5;
		internal const int BREAKPOINT = 6;
		internal const int STEP = 7;
		internal const int EXCEPTION = 8;
		internal const int KEEPALIVE = 9;
		internal const int USER_BREAK = 10;
		internal const int USER_LOG = 11;
		internal const int MODULE_LOAD = 12;
		internal const int MODULE_UNLOAD = 13;
	}

	static class EventModifierKind
	{
		internal const int BreakpointLocation = 50;
	}

	static class SuspendPolicy
	{
		internal const int NONE = 0;
		internal const int EVENT_THREAD = 1;
		internal const int ALL = 2;
	}
}