using System.Collections.Generic;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class TypeRef
	{
		public string ModuleName;

		public int ClassToken;

		public string VmQName;

		public bool IsPointer;

		public bool IsByRef;

		public List<int> ArraySizes;

		public List<int> ArrayLowerBounds;

		public TypeRef()
		{
		}

		public TypeRef(MetadataTypeInfo type)
		{
			ModuleName = type.MetadataImport.ModuleName;
			ClassToken = type.MetadataToken;
			VmQName = type.FullName;
			IsPointer = type.IsPointer;
			IsByRef = type.IsByRef;
			ArraySizes = type.m_arraySizes;
			ArrayLowerBounds = type.m_arrayLoBounds;
		}

		public TypeRef(string moduleName, int classToken, string vmQName)
		{
			ModuleName = moduleName;
			ClassToken = classToken;
			VmQName = vmQName;
		}
	}
}