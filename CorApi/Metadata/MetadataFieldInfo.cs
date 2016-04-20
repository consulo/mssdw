//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Microsoft.Samples.Debugging.Extensions;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Microsoft.Samples.Debugging.CorMetadata
{
	public sealed class MetadataFieldInfo
	{
		private MetadataTypeInfo myFieldType;

		private IMetadataImport m_importer;
		private int m_fieldToken;
		private MetadataTypeInfo m_declaringType;

		private string m_name;
		private FieldAttributes m_fieldAttributes;
		private object m_value;
		// [Xamarin] Expression evaluator.
		private object[] m_customAttributes;

		internal MetadataFieldInfo(CorMetadataImport corMetadataImport, IMetadataImport importer, int fieldToken, MetadataTypeInfo declaringType)
		{
			m_importer = importer;
			m_fieldToken = fieldToken;
			m_declaringType = declaringType;

			// Initialize
			int mdTypeDef;
			int pchField, pcbSigBlob, pdwCPlusTypeFlab, pcchValue, pdwAttr;
			IntPtr ppvSigBlob;
			IntPtr ppvRawValue;
			m_importer.GetFieldProps(m_fieldToken,
					out mdTypeDef,
					null,
					0,
					out pchField,
					out pdwAttr,
					out ppvSigBlob,
					out pcbSigBlob,
					out pdwCPlusTypeFlab,
					out ppvRawValue,
					out pcchValue
			);

			StringBuilder szField = new StringBuilder(pchField);
			m_importer.GetFieldProps(m_fieldToken,
					out mdTypeDef,
					szField,
					szField.Capacity,
					out pchField,
					out pdwAttr,
					out ppvSigBlob,
					out pcbSigBlob,
					out pdwCPlusTypeFlab,
					out ppvRawValue,
					out pcchValue
			);
			m_fieldAttributes = (FieldAttributes)pdwAttr;
			m_name = szField.ToString();

			// Get the values for static literal fields with primitive types
			FieldAttributes staticLiteralField = FieldAttributes.Static | FieldAttributes.HasDefault | FieldAttributes.Literal;
			if((m_fieldAttributes & staticLiteralField) == staticLiteralField)
			{
				m_value = ParseDefaultValue(declaringType, ppvSigBlob, ppvRawValue);
			}

			myFieldType = MetadataHelperFunctionsExtensions.ReadType(corMetadataImport, m_importer, ref ppvSigBlob);

			// [Xamarin] Expression evaluator.
			MetadataHelperFunctionsExtensions.GetCustomAttribute(m_importer, m_fieldToken, typeof (DebuggerBrowsableAttribute));
		}

		private static object ParseDefaultValue(MetadataTypeInfo declaringType, IntPtr ppvSigBlob, IntPtr ppvRawValue)
		{
			IntPtr ppvSigTemp = ppvSigBlob;
			CorCallingConvention callingConv = MetadataHelperFunctions.CorSigUncompressCallingConv(ref ppvSigTemp);
			Debug.Assert(callingConv == CorCallingConvention.Field);

			CorElType elementType = MetadataHelperFunctions.CorSigUncompressElementType(ref ppvSigTemp);
			if(elementType == CorElType.ELEMENT_TYPE_VALUETYPE)
			{
				uint token = MetadataHelperFunctions.CorSigUncompressToken(ref ppvSigTemp);

				if(token == declaringType.MetadataToken)
				{
					// Static literal field of the same type as the enclosing type
					// may be one of the value fields of an enum
					if(declaringType.ReallyIsEnum)
					{
						// If so, the value will be of the enum's underlying type,
						// so we change it from VALUETYPE to be that type so that
						// the following code will get the value
						elementType = declaringType.EnumUnderlyingType;
					}
				}
			}

			switch(elementType)
			{
				case CorElType.ELEMENT_TYPE_CHAR:
					return (char)Marshal.ReadByte(ppvRawValue);
				case CorElType.ELEMENT_TYPE_I1:
					return (sbyte)Marshal.ReadByte(ppvRawValue);
				case CorElType.ELEMENT_TYPE_U1:
					return Marshal.ReadByte(ppvRawValue);
				case CorElType.ELEMENT_TYPE_I2:
					return Marshal.ReadInt16(ppvRawValue);
				case CorElType.ELEMENT_TYPE_U2:
					return (ushort)Marshal.ReadInt16(ppvRawValue);
				case CorElType.ELEMENT_TYPE_I4:
					return Marshal.ReadInt32(ppvRawValue);
				case CorElType.ELEMENT_TYPE_U4:
					return (uint)Marshal.ReadInt32(ppvRawValue);
				case CorElType.ELEMENT_TYPE_I8:
					return Marshal.ReadInt64(ppvRawValue);
				case CorElType.ELEMENT_TYPE_U8:
					return (ulong)Marshal.ReadInt64(ppvRawValue);
				case CorElType.ELEMENT_TYPE_I:
					return Marshal.ReadIntPtr(ppvRawValue);
				case CorElType.ELEMENT_TYPE_U:
				case CorElType.ELEMENT_TYPE_R4:
				case CorElType.ELEMENT_TYPE_R8:
					// Technically U and the floating-point ones are options in the CLI, but not in the CLS or C#, so these are NYI
				default:
					return null;
			}
		}

		public object GetValue(object obj)
		{
			FieldAttributes staticLiteralField = FieldAttributes.Static | FieldAttributes.HasDefault | FieldAttributes.Literal;
			if((m_fieldAttributes & staticLiteralField) != staticLiteralField)
			{
				throw new InvalidOperationException("Field is not a static literal field.");
			}
			if(m_value == null)
			{
				throw new NotImplementedException("GetValue not implemented for the given field type.");
			}
			else
			{
				return m_value;
			}
		}

		// [Xamarin] Expression evaluator.
		public object[] GetCustomAttributes(bool inherit)
		{
			if(m_customAttributes == null)
				m_customAttributes = MetadataHelperFunctionsExtensions.GetDebugAttributes(m_importer, m_fieldToken);
			return m_customAttributes;
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
		public bool IsDefined(Type attributeType, bool inherit)
		{
			return GetCustomAttributes(attributeType, inherit).Length > 0;
		}

		public MetadataTypeInfo FieldType
		{
			get
			{
				return myFieldType;
			}
		}


		public FieldAttributes Attributes
		{
			get
			{
				return m_fieldAttributes;
			}
		}

		public string Name
		{
			get
			{
				return m_name;
			}
		}

		public int MetadataToken
		{
			get
			{
				return m_fieldToken;
			}
		}
	}
}
