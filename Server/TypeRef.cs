using System;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class TypeRef
	{
		public int ModuleToken;

		public int ClassToken;

		//public string VmQName;

		public TypeRef()
		{
		}

		public TypeRef(Type type)
		{
			if(type is MetadataType)
			{
				ModuleToken = ((MetadataType) type).CorMetadataImport.ModuleToken;
				ClassToken = ((MetadataType) type).MetadataToken;
			}
			else
			{
				throw new Exception("We cant send not metadata type: " + type.FullName);
			}
		}

		public TypeRef(int moduleToken, int classToken)
		{
			ModuleToken = moduleToken;
			ClassToken = classToken;
		}
	}
}