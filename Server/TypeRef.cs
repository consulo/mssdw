using System;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class TypeRef
	{
		public string ModuleName;

		public int ClassToken;

		public string VmQName;

		public TypeRef()
		{
		}

		public TypeRef(Type type)
		{
			if(type is MetadataType)
			{
				ModuleName = ((MetadataType) type).CorMetadataImport.ModuleName;
				ClassToken = ((MetadataType) type).MetadataToken;
				VmQName = type.FullName;
			}
			else
			{
				throw new Exception("We cant send not metadata type: " + type.FullName);
			}
		}

		public TypeRef(string moduleName, int classToken, string vmQName)
		{
			ModuleName = moduleName;
			ClassToken = classToken;
			VmQName = vmQName;
		}
	}
}