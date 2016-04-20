using System;
using System.Reflection;
using System.Text;
using System.Globalization;
using System.Collections;

using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Microsoft.Samples.Debugging.Extensions;
using System.Collections.Generic;

namespace Microsoft.Samples.Debugging.CorMetadata
{
	public sealed class MetadataMethodInfo
	{
		public readonly CorMetadataImport CorMetadataImport;
		private IMetadataImport m_importer;
		private string m_name;
		private int m_classToken;
		private int m_methodToken;
		private MethodAttributes m_methodAttributes;
		// [Xamarin] Expression evaluator.
		private List<MetadataTypeInfo> m_argTypes;
		private MetadataTypeInfo m_retType;
		private object[] m_customAttributes;

		internal MetadataMethodInfo(CorMetadataImport corMetadataImport, IMetadataImport importer, int methodToken)
		{
			CorMetadataImport = corMetadataImport;
			if(!importer.IsValidToken((uint)methodToken))
				throw new ArgumentException();

			m_importer = importer;
			m_methodToken = methodToken;

			int size;
			uint pdwAttr;
			IntPtr ppvSigBlob;
			uint pulCodeRVA, pdwImplFlags;
			uint pcbSigBlob;

			m_importer.GetMethodProps((uint)methodToken,
					out m_classToken,
					null,
					0,
					out size,
					out pdwAttr,
					out ppvSigBlob,
					out pcbSigBlob,
					out pulCodeRVA,
					out pdwImplFlags);

			StringBuilder szMethodName = new StringBuilder(size);
			m_importer.GetMethodProps((uint)methodToken,
					out m_classToken,
					szMethodName,
					szMethodName.Capacity,
					out size,
					out pdwAttr,
					out ppvSigBlob,
					out pcbSigBlob,
					out pulCodeRVA,
					out pdwImplFlags);

			// [Xamarin] Expression evaluator.
			CorCallingConvention callingConv;
			MetadataHelperFunctionsExtensions.ReadMethodSignature(CorMetadataImport, importer, ref ppvSigBlob, out callingConv, out m_retType, out m_argTypes);
			m_name = szMethodName.ToString();
			m_methodAttributes = (MethodAttributes)pdwAttr;
		}

		// [Xamarin] Expression evaluator.
		public MetadataTypeInfo ReturnType
		{
			get
			{
				return m_retType;
			}
		}

		public MetadataTypeInfo DeclaringType
		{
			get
			{
				if(TokenUtils.IsNullToken(m_classToken))
					return null;                            // this is method outside of class

				return new MetadataTypeInfo(CorMetadataImport, m_importer, m_classToken);
			}
		}

		public string Name
		{
			get
			{
				return m_name;
			}
		}

		public MethodAttributes Attributes
		{
			get
			{
				return m_methodAttributes;
			}
		}

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
				m_customAttributes = MetadataHelperFunctionsExtensions.GetDebugAttributes(m_importer, m_methodToken);
			return m_customAttributes;
		}

		// [Xamarin] Expression evaluator.
		public MetadataParameterInfo[] GetParameters()
		{
			List<MetadataParameterInfo> al = new List<MetadataParameterInfo>();
			IntPtr hEnum = new IntPtr();
			int nArg = 0;
			try
			{
				while(true)
				{
					uint count;
					int paramToken;
					m_importer.EnumParams(ref hEnum, m_methodToken, out paramToken, 1, out count);
					if(count != 1)
						break;
					var mp = new MetadataParameterInfo(CorMetadataImport, m_importer, paramToken, this, m_argTypes[nArg++]);
					if(mp.Name != string.Empty)
						al.Add(mp);
				}
			}
			finally
			{
				m_importer.CloseEnum(hEnum);
			}
			return al.ToArray();
		}

		public int MetadataToken
		{
			get
			{
				return m_methodToken;
			}
		}

		public string[] GetGenericArgumentNames()
		{
			return MetadataHelperFunctions.GetGenericArgumentNames(m_importer, m_methodToken);
		}
	}
}