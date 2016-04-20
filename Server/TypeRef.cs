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

		public TypeRef(MetadataTypeInfo type)
		{
			ModuleName = type.CorMetadataImport.ModuleName;
			ClassToken = type.MetadataToken;
			VmQName = type.FullName;
		}

		public TypeRef(string moduleName, int classToken, string vmQName)
		{
			ModuleName = moduleName;
			ClassToken = classToken;
			VmQName = vmQName;
		}
	}
}