using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class OnModuleLoadEvent
	{
		public string ModuleFile;

		public OnModuleLoadEvent(string file)
		{
			ModuleFile = file;
		}
	}
}