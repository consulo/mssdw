using System;
using System.Collections.Generic;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class TypeRef
	{
		public string ModuleName;

		public int ClassToken;

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
			IsPointer = type.IsPointer;
			IsByRef = type.IsByRef;
			ArraySizes = type.m_arraySizes;
			ArrayLowerBounds = type.m_arrayLoBounds;
		}

		public TypeRef(string moduleName, int classToken)
		{
			ModuleName = moduleName;
			ClassToken = classToken;
		}


		public override string ToString()
		{
			return string.Format("ModuleName: {0}, ClassToken: {1}", ModuleName, ClassToken);
		}
	}
}