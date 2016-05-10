//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Consulo.Internal.Mssdw;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Microsoft.Samples.Debugging.Extensions;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Microsoft.Samples.Debugging.CorMetadata
{
	public sealed class MetadataTypeInfo
	{
		// Sorts KeyValuePair<string,ulong>'s in increasing order by the value
		class AscendingValueComparer<K, V> : IComparer<KeyValuePair<K, V>> where V : IComparable
		{
			public int Compare(KeyValuePair<K, V> p1, KeyValuePair<K, V> p2)
			{
				return p1.Value.CompareTo(p2.Value);
			}

			public bool Equals(KeyValuePair<K, V> p1, KeyValuePair<K, V> p2)
			{
				return Compare(p1, p2) == 0;
			}

			public int GetHashCode(KeyValuePair<K, V> p)
			{
				return p.Value.GetHashCode();
			}
		}

		// member variables
		private string m_name;
		private IMetadataImport m_importer;
		private int m_typeToken;
		private bool m_isEnum;
		private bool m_isFlagsEnum;
		private CorElType m_enumUnderlyingType;
		private List<KeyValuePair<string, ulong>> m_enumValues;
		// [Xamarin] Expression evaluator.
		private object[] m_customAttributes;
		private MetadataTypeInfo m_declaringType;
		private TypeAttributes myTypeAttributes;
		internal List<int> m_arraySizes;
		internal List<int> m_arrayLoBounds;
		internal bool m_isByRef, m_isPtr;
		internal List<MetadataTypeInfo> m_typeArgs;
		private string baseTypeName;

		public readonly CorMetadataImport MetadataImport;

		internal MetadataTypeInfo(CorMetadataImport corMetadataImport, IMetadataImport importer, int classToken)
		{
			Debug.Assert(importer != null);

			MetadataImport = corMetadataImport;
			m_importer = importer;
			m_typeToken = classToken;

			if( classToken == 0 )
			{
				// classToken of 0 represents a special type that contains
				// fields of global parameters.
				m_name = "";
			}
			else
			{
				StringBuilder szTypedef = null;
				// get info about the type
				int size;
				int ptkExtends;
				TypeAttributes pdwTypeDefFlags;
				importer.GetTypeDefProps(classToken,
						null,
						0,
						out size,
						out pdwTypeDefFlags,
						out ptkExtends
				);
				if(size == 0)
				{
					int ptkResScope = 0;
					importer.GetTypeRefProps(classToken,
							out ptkResScope,
							null,
							0,
							out size
					);
					szTypedef = new StringBuilder(size);
					importer.GetTypeRefProps(classToken,
							out ptkResScope,
							szTypedef,
							size,
							out size
					);
				}
				else
				{
					szTypedef = new StringBuilder(size);
					importer.GetTypeDefProps(classToken,
							szTypedef,
							szTypedef.Capacity,
							out size,
							out pdwTypeDefFlags,
							out ptkExtends
					);
				}
				m_name = GetNestedClassPrefix(importer, classToken, pdwTypeDefFlags) + szTypedef.ToString();
				myTypeAttributes = pdwTypeDefFlags;
				// Check whether the type is an enum
				baseTypeName = GetTypeName(importer, ptkExtends);

				IntPtr ppvSig;
				if(baseTypeName == "System.Enum")
				{
					m_isEnum = true;
					m_enumUnderlyingType = GetEnumUnderlyingType(importer, classToken);

					// Check for flags enum by looking for FlagsAttribute
					uint sigSize = 0;
					ppvSig = IntPtr.Zero;
					int hr = importer.GetCustomAttributeByName(classToken, "System.FlagsAttribute", out ppvSig, out sigSize);
					if(hr < 0)
					{
						throw new COMException("Exception looking for flags attribute", hr);
					}
					m_isFlagsEnum = (hr == 0);  // S_OK means the attribute is present.
				}
			}
		}

		// [Xamarin] Expression evaluator.
		public MetadataTypeInfo DeclaringType
		{
			get
			{
				return m_declaringType;
			}
		}

		public TypeAttributes Attributes
		{
			get
			{
				return myTypeAttributes;
			}
		}

		private static string GetTypeName(IMetadataImport importer, int tk)
		{
			// Get the base type name
			StringBuilder sbBaseName = new StringBuilder();
			MetadataToken token = new MetadataToken(tk);
			int size;
			TypeAttributes pdwTypeDefFlags;
			int ptkExtends;

			if(token.IsOfType(MetadataTokenType.TypeDef))
			{
				importer.GetTypeDefProps(token,
						null,
						0,
						out size,
						out pdwTypeDefFlags,
						out ptkExtends
				);
				sbBaseName.Capacity = size;
				importer.GetTypeDefProps(token,
						sbBaseName,
						sbBaseName.Capacity,
						out size,
						out pdwTypeDefFlags,
						out ptkExtends
				);
			}
			else if(token.IsOfType(MetadataTokenType.TypeRef))
			{
				// Some types extend TypeRef 0x02000000 as a special-case
				// But that token does not exist so we can't get a name for it
				if(token.Index != 0)
				{
					int resolutionScope;
					importer.GetTypeRefProps(token,
							out resolutionScope,
							null,
							0,
							out size
					);
					sbBaseName.Capacity = size;
					importer.GetTypeRefProps(token,
							out resolutionScope,
							sbBaseName,
							sbBaseName.Capacity,
							out size
					);
				}
			}
			// Note the base type can also be a TypeSpec token, but that only happens
			// for arrays, generics, that sort of thing. In this case, we'll leave the base
			// type name stringbuilder empty, and thus know it's not an enum.

			return sbBaseName.ToString();
		}

		private static CorElType GetEnumUnderlyingType(IMetadataImport importer, int tk)
		{
			IntPtr hEnum = IntPtr.Zero;
			int mdFieldDef;
			uint numFieldDefs;
			int fieldAttributes;
			int nameSize;
			int cPlusTypeFlab;
			IntPtr ppValue;
			int pcchValue;
			IntPtr ppvSig;
			int size;
			int classToken;

			importer.EnumFields(ref hEnum, tk, out mdFieldDef, 1, out numFieldDefs);
			while(numFieldDefs != 0)
			{
				importer. GetFieldProps(mdFieldDef, out classToken, null, 0, out nameSize, out fieldAttributes, out ppvSig, out size, out cPlusTypeFlab, out ppValue, out pcchValue);
				Debug.Assert(tk == classToken);

				// Enums should have one instance field that indicates the underlying type
				if((((FieldAttributes)fieldAttributes) & FieldAttributes.Static) == 0)
				{
					Debug.Assert(size == 2); // Primitive type field sigs should be two bytes long

					IntPtr ppvSigTemp = ppvSig;
					CorCallingConvention callingConv = MetadataHelperFunctions.CorSigUncompressCallingConv(ref ppvSigTemp);
					Debug.Assert(callingConv == CorCallingConvention.Field);

					return MetadataHelperFunctions.CorSigUncompressElementType(ref ppvSigTemp);
				}

				importer.EnumFields(ref hEnum, tk, out mdFieldDef, 1, out numFieldDefs);
			}

			Debug.Fail("Should never get here.");
			throw new ArgumentException("Non-enum passed to GetEnumUnderlyingType.");
		}

		public CorValue GetFieldValue(MetadataFieldInfo fieldInfo, CorFrame corFrame)
		{
			CorModule metadataImportModule = MetadataImport.Module;

			CorClass corClass = metadataImportModule.GetClassFromToken(m_typeToken);

			return corClass.GetStaticFieldValue(fieldInfo.MetadataToken, corFrame);
		}

		// properties
		public int MetadataToken
		{
			get
			{
				return m_typeToken;
			}
		}

		// [Xamarin] Expression evaluator.
		public string Name
		{
			get
			{
				int i = m_name.LastIndexOf('+');
				if(i == -1)
					i = m_name.LastIndexOf('.');
				if(i != -1)
					return m_name.Substring(i + 1);
				else
					return m_name;
			}
		}

		public Type UnderlyingSystemType
		{
			get
			{
				return Type.GetType(FullName);
			}
		}

		public TypeRef BaseType(DebugSession debugSession)
		{
			//TODO [VISTALL] find better way for base type calculation, old impl was return BAD module name - returned this module ref,
			//TODO [VISTALL] but not target module ref. For example:
			//TODO [VISTALL] Type 'Program:untitled.exe' return base 'System.Object:untitled.exe'. Second module name is invalid, required 'mscorlib.dll'

			// in this case we return null for now
			if(IsArray || IsPointer || IsByRef)
			{
				return null;
			}

			if(baseTypeName == null || baseTypeName.Length == 0)
			{
				return null;
			}
			TypeRef typeRef = debugSession.FindTypeByName(baseTypeName);
			if(typeRef == null)
			{
				return null;
			}
			return typeRef;
			/*if(m_typeToken == 0)
			{
				throw new NotImplementedException();
			}

			var token = new MetadataToken(m_typeToken);
			int size;
			TypeAttributes pdwTypeDefFlags;
			int ptkExtends;

			m_importer.GetTypeDefProps(token,
					null,
					0,
					out size,
					out pdwTypeDefFlags,
					out ptkExtends
			);

			if(ptkExtends == 0)
			{
				return null;
			}

			return new MetadataTypeInfo(MetadataImport, m_importer, ptkExtends);*/
		}

		// [Xamarin] Expression evaluator.
		public string Namespace
		{
			get
			{
				int i = m_name.LastIndexOf('.');
				if(i != -1)
					return m_name.Substring(0, i);
				else
					return "";
			}
		}

		// [Xamarin] Expression evaluator.
		public string FullName
		{
			get
			{
				StringBuilder sb = new StringBuilder(m_name);
				if(m_typeArgs != null)
				{
					sb.Append("[");
					for(int n = 0; n < m_typeArgs.Count; n++)
					{
						if(n > 0)
							sb.Append(",");
						sb.Append(m_typeArgs[n].FullName);
					}
					sb.Append("]");
				}
				if(IsPointer)
					sb.Append("*");
				if(IsArray)
				{
					sb.Append("[");
					for(int n = 1; n < m_arraySizes.Count; n++)
						sb.Append(",");
					sb.Append("]");
				}
				return sb.ToString();
			}
		}


		// [Xamarin] Expression evaluator.
		public MetadataTypeInfo[] GetGenericArguments()
		{
			return m_typeArgs.ToArray();
		}

		// methods

		// [Xamarin] Expression evaluator.
		public bool IsDefined(Type attributeType, bool inherit)
		{
			return GetCustomAttributes(attributeType, inherit).Length > 0;
		}

		// [Xamarin] Expression evaluator.
		public object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			ArrayList list = new ArrayList();
			foreach (object ob in GetCustomAttributes(inherit))
			{
				if(attributeType.IsInstanceOfType(ob))
					list.Add(ob);
			}
			return list.ToArray();
		}

		// [Xamarin] Expression evaluator.
		public object[] GetCustomAttributes(bool inherit)
		{
			if(m_customAttributes == null)
				m_customAttributes = MetadataHelperFunctionsExtensions.GetDebugAttributes(m_importer, m_typeToken);
			return m_customAttributes;
		}

		public bool IsPointer
		{
			get
			{
				return m_isPtr;
			}
		}

		public bool IsByRef
		{
			get
			{
				return m_isByRef;
			}
		}

		public bool IsArray
		{
			get
			{
				return m_arraySizes != null;
			}
		}

		public int GetArrayRank()
		{
			if(m_arraySizes != null)
				return m_arraySizes.Count;
			else
				return 0;
		}

		public MetadataPropertyInfo[] GetProperties()
		{
			if(IsArray || IsPointer || IsByRef)
			{
				return Array.Empty<MetadataPropertyInfo>();
			}

			List<MetadataPropertyInfo> al = new List<MetadataPropertyInfo>();
			var hEnum = new IntPtr();

			int propertyToken;
			try
			{
				while(true)
				{
					uint size;
					m_importer.EnumProperties(ref hEnum, (int) m_typeToken, out propertyToken, 1, out size);
					if(size == 0)
						break;
					var prop = new MetadataPropertyInfo(MetadataImport, m_importer, propertyToken, this);
					try
					{
						MetadataMethodInfo mi = prop.GetGetMethod() ?? prop.GetSetMethod();
						if(mi == null)
							continue;
						al.Add(prop);
					}
					catch
					{
						// Ignore
					}
				}
			}
			finally
			{
				m_importer.CloseEnum(hEnum);
			}

			return al.ToArray();
		}

		public Type[] GetInterfaces()
		{
			var al = new ArrayList();
			var hEnum = new IntPtr();

			int impl;
			try
			{
				while(true)
				{
					uint size;
					m_importer.EnumInterfaceImpls(ref hEnum, (int)m_typeToken, out impl, 1, out size);
					if(size == 0)
						break;
					int classTk;
					int intfTk;
					m_importer.GetInterfaceImplProps(impl, out classTk, out intfTk);
					al.Add(new MetadataTypeInfo(MetadataImport, m_importer, intfTk));
				}
			}
			finally
			{
				m_importer.CloseEnum(hEnum);
			}
			return (Type[]) al.ToArray(typeof(Type));
		}

		public MetadataFieldInfo[] GetFields()
		{
			if(IsArray || IsPointer || IsByRef)
			{
				return Array.Empty<MetadataFieldInfo>();
			}

			List<MetadataFieldInfo> al = new List<MetadataFieldInfo>();
			var hEnum = new IntPtr();

			int fieldToken;
			try
			{
				while(true)
				{
					uint size;
					m_importer.EnumFields(ref hEnum, (int)m_typeToken, out fieldToken, 1, out size);
					if(size == 0)
						break;
					// [Xamarin] Expression evaluator.
					MetadataFieldInfo field = new MetadataFieldInfo(MetadataImport, m_importer, fieldToken, this);
					al.Add(field);
				}
			}
			finally
			{
				m_importer.CloseEnum(hEnum);
			}
			return al.ToArray();
		}

		public MetadataMethodInfo[] GetMethods()
		{
			if(IsArray || IsPointer || IsByRef)
			{
				return Array.Empty<MetadataMethodInfo>();
			}

			List<MetadataMethodInfo> al = new List<MetadataMethodInfo>();
			IntPtr hEnum = new IntPtr();

			int methodToken;
			try
			{
				while(true)
				{
					int size;
					m_importer.EnumMethods(ref hEnum, (int)m_typeToken, out methodToken, 1, out size);
					if(size == 0)
						break;
					// [Xamarin] Expression evaluator.
					var met = new MetadataMethodInfo(MetadataImport, m_importer, methodToken);
					al.Add(met);
				}
			}
			finally
			{
				m_importer.CloseEnum(hEnum);
			}
			return al.ToArray();
		}

		public string[] GetGenericArgumentNames()
		{
			return MetadataHelperFunctions.GetGenericArgumentNames(m_importer, m_typeToken);
		}

		public bool ReallyIsEnum
		{
			get
			{
				return m_isEnum;
			}
		}

		public bool ReallyIsFlagsEnum
		{
			get
			{
				return m_isFlagsEnum;
			}
		}

		public CorElType EnumUnderlyingType
		{
			get
			{
				return m_enumUnderlyingType;
			}
		}

		[CLSCompliant(false)]
		public IList<KeyValuePair<string, ulong>> EnumValues
		{
			get
			{
				if(m_enumValues == null)
				{
					// Build a big list of field values
					MetadataFieldInfo[] fields = GetFields();       // BindingFlags is actually ignored in the "fake" type,
					// but we only want the public fields anyway
					m_enumValues = new List<KeyValuePair<string, ulong>>();
					for(int i = 0; i < fields.Length; i++)
					{
						MetadataFieldInfo field = fields[i] as MetadataFieldInfo;
						if(field.IsConstant)
						{
							m_enumValues.Add(new KeyValuePair<string, ulong>(field.Name, Convert.ToUInt64(field.ConstantValue, CultureInfo.InvariantCulture)));
						}
					}

					AscendingValueComparer<string, ulong> comparer = new AscendingValueComparer<string, ulong>();
					m_enumValues.Sort(comparer);
				}

				return m_enumValues;
			}
		}

		// returns "" for normal classes, returns prefix for nested classes
		private string GetNestedClassPrefix(IMetadataImport importer, int classToken, TypeAttributes attribs)
		{
			if( (attribs & TypeAttributes.VisibilityMask) > TypeAttributes.Public )
			{
				// it is a nested class
				int enclosingClass;
				importer.GetNestedClassProps(classToken, out enclosingClass);
				// [Xamarin] Expression evaluator.
				m_declaringType = new MetadataTypeInfo(MetadataImport, importer, enclosingClass);
				return m_declaringType.FullName + "+";
				//MetadataType mt = new MetadataType(importer,enclosingClass);
				//return mt.Name+".";
			}
			else
				return string.Empty;
		}
	}
}
 
