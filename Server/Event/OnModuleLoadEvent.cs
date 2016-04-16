using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class OnModuleLoadEvent
	{
		public String ModuleFile;

		public OnModuleLoadEvent(String file)
		{
			ModuleFile = file;
		}
	}
}