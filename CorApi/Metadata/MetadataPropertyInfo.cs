using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Microsoft.Samples.Debugging.Extensions;

namespace Microsoft.Samples.Debugging.CorMetadata
{
	public class MetadataPropertyInfo
	{
		private IMetadataImport m_importer;
		private int m_propertyToken;
		private MetadataTypeInfo m_declaringType;
		private object[] m_customAttributes;

		private string m_name;
		private PropertyAttributes m_propAttributes;

		int m_pmdSetter;
		int m_pmdGetter;

		MetadataMethodInfo mySetter;
		MetadataMethodInfo myGetter;

		public CorMetadataImport CorMetadataImport;

		internal MetadataPropertyInfo(CorMetadataImport corMetadataImport, IMetadataImport importer, int propertyToken, MetadataTypeInfo declaringType)
		{
			CorMetadataImport = corMetadataImport;
			m_importer = importer;
			m_propertyToken = propertyToken;
			m_declaringType = declaringType;

			int mdTypeDef;
			int pchProperty;
			int pdwPropFlags;
			IntPtr ppvSig;
			int pbSig;
			int pdwCPlusTypeFlag;
			IntPtr ppDefaultValue;
			int pcchDefaultValue;
			int rmdOtherMethod;
			int pcOtherMethod;

			m_importer.GetPropertyProps(
					m_propertyToken,
					out mdTypeDef,
					null,
					0,
					out pchProperty,
					out pdwPropFlags,
					out ppvSig,
					out pbSig,
					out pdwCPlusTypeFlag,
					out ppDefaultValue,
					out pcchDefaultValue,
					out m_pmdSetter,
					out m_pmdGetter,
					out rmdOtherMethod,
					0,
					out pcOtherMethod);

			StringBuilder szProperty = new StringBuilder(pchProperty);
			m_importer.GetPropertyProps(
					m_propertyToken,
					out mdTypeDef,
					szProperty,
					pchProperty,
					out pchProperty,
					out pdwPropFlags,
					out ppvSig,
					out pbSig,
					out pdwCPlusTypeFlag,
					out ppDefaultValue,
					out pcchDefaultValue,
					out m_pmdSetter,
					out m_pmdGetter,
					out rmdOtherMethod,
					0,
					out pcOtherMethod);
			m_propAttributes = (PropertyAttributes) pdwPropFlags;
			m_name = szProperty.ToString();
			MetadataHelperFunctionsExtensions.GetCustomAttribute(importer, propertyToken, typeof (System.Diagnostics.DebuggerBrowsableAttribute));

			if(!m_importer.IsValidToken((uint)m_pmdGetter))
				m_pmdGetter = 0;

			if(!m_importer.IsValidToken((uint)m_pmdSetter))
				m_pmdSetter = 0;
		}

		public PropertyAttributes Attributes
		{
			get
			{
				return m_propAttributes;
			}
		}

		public bool CanRead
		{
			get
			{
				return m_pmdGetter != 0;
			}
		}

		public bool CanWrite
		{
			get
			{
				return m_pmdSetter != 0;
			}
		}

		public MetadataMethodInfo GetGetMethod()
		{
			if(m_pmdGetter == 0)
				return null;

			if(myGetter == null)
				myGetter = new MetadataMethodInfo(CorMetadataImport, m_importer, m_pmdGetter);

			return myGetter;
		}

		public MetadataParameterInfo[] GetIndexParameters()
		{
			MetadataMethodInfo mi = GetGetMethod();
			if(mi == null)
				return new MetadataParameterInfo[0];
			return mi.GetParameters();
		}

		public MetadataMethodInfo GetSetMethod()
		{
			if(m_pmdSetter == 0)
				return null;

			if(mySetter == null)
				mySetter = new MetadataMethodInfo(CorMetadataImport, m_importer, m_pmdSetter);

			return mySetter;
		}

		public MetadataTypeInfo PropertyType
		{
			get
			{
				if(myGetter != null)
				{
					return myGetter.ReturnType;
				}
				else
				{
					return mySetter.GetParameters()[0].ParameterType;
				}
			}
		}

		public MetadataTypeInfo DeclaringType
		{
			get
			{
				return m_declaringType;
			}
		}

		public bool IsDefined(Type attributeType, bool inherit)
		{
			return GetCustomAttributes(attributeType, inherit).Length > 0;
		}

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

		public object[] GetCustomAttributes(bool inherit)
		{
			if(m_customAttributes == null)
				m_customAttributes = MetadataHelperFunctionsExtensions.GetDebugAttributes(m_importer, m_propertyToken);
			return m_customAttributes;
		}

		public int MetadataToken
		{
			get
			{
				return m_propertyToken;
			}
		}

		public string Name
		{
			get
			{
				return m_name;
			}
		}
	}
}
