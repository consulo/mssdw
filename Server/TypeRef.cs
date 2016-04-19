using System;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class TypeRef
	{
		public int ModuleToken;

		public int ClassToken;

		public string VmQName;

		public TypeRef()
		{
		}

		public TypeRef(Type type)
		{
			if(type is MetadataType)
			{
				ModuleToken = ((MetadataType) type).CorMetadataImport.ModuleToken;
				ClassToken = ((MetadataType) type).MetadataToken;
				VmQName = type.FullName;
			}
			else
			{
				throw new Exception("We cant send not metadata type: " + type.FullName);
			}
		}

		public TypeRef(int moduleToken, int classToken, string vmQName)
		{
			ModuleToken = moduleToken;
			ClassToken = classToken;
			VmQName = vmQName;
		}
	}
}