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
	public sealed class MetadataMethodInfo : MethodInfo
	{
		public CorMetadataImport CorMetadataImport
		{
			get;
			private set;
		}

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
		public override Type ReturnType
		{
			get
			{
				return m_retType;
			}
		}

		public override ICustomAttributeProvider ReturnTypeCustomAttributes
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override Type ReflectedType
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override Type DeclaringType
		{
			get
			{
				if(TokenUtils.IsNullToken(m_classToken))
					return null;                            // this is method outside of class

				return new MetadataType(CorMetadataImport, m_importer, m_classToken);
			}
		}

		public override string Name
		{
			get
			{
				return m_name;
			}
		}

		public override MethodAttributes Attributes
		{
			get
			{
				return m_methodAttributes;
			}
		}

		public override RuntimeMethodHandle MethodHandle
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override MethodInfo GetBaseDefinition()
		{
			throw new NotImplementedException();
		}

		// [Xamarin] Expression evaluator.
		public override bool IsDefined(Type attributeType, bool inherit)
		{
			return GetCustomAttributes(attributeType, inherit).Length > 0;
		}

		// [Xamarin] Expression evaluator.
		public override object[] GetCustomAttributes(Type attributeType, bool inherit)
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
		public override object[] GetCustomAttributes(bool inherit)
		{
			if(m_customAttributes == null)
				m_customAttributes = MetadataHelperFunctionsExtensions.GetDebugAttributes(m_importer, m_methodToken);
			return m_customAttributes;
		}

		public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.MethodImplAttributes GetMethodImplementationFlags()
		{
			throw new NotImplementedException();
		}

		// [Xamarin] Expression evaluator.
		public override System.Reflection.ParameterInfo[] GetParameters()
		{
			ArrayList al = new ArrayList();
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
			return (ParameterInfo[]) al.ToArray(typeof(ParameterInfo));
		}

		public override int MetadataToken
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

		private IMetadataImport m_importer;
		private string m_name;
		private int m_classToken;
		private int m_methodToken;
		private MethodAttributes m_methodAttributes;
		// [Xamarin] Expression evaluator.
		private List<Type> m_argTypes;
		private Type m_retType;
		private object[] m_customAttributes;
	}
}