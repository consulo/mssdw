//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Reflection;
using System.Text;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;

namespace Microsoft.Samples.Debugging.CorMetadata
{
	public sealed class MetadataParameterInfo
	{
		private MetadataTypeInfo myType;
		private string myName;
		private int myPosition;
		private MetadataMethodInfo myMethod;
		private ParameterAttributes myAttributes;

		internal MetadataParameterInfo(CorMetadataImport corMetadataImport, IMetadataImport importer, int paramToken, MetadataMethodInfo memberImpl, MetadataTypeInfo typeImpl)
		{
			int parentToken;
			uint pulSequence, pdwAttr, pdwCPlusTypeFlag, pcchValue, size;

			IntPtr ppValue;
			importer.GetParamProps(paramToken,
					out parentToken,
					out pulSequence,
					null,
					0,
					out size,
					out pdwAttr,
					out pdwCPlusTypeFlag,
					out ppValue,
					out pcchValue
			);
			StringBuilder szName = new StringBuilder((int)size);
			importer.GetParamProps(paramToken,
					out parentToken,
					out pulSequence,
					szName,
					(uint)szName.Capacity,
					out size,
					out pdwAttr,
					out pdwCPlusTypeFlag,
					out ppValue,
					out pcchValue
			);
			myName = szName.ToString();
			myType = typeImpl;
			myPosition = (int)pulSequence;
			myAttributes = (ParameterAttributes)pdwAttr;
			myMethod = memberImpl;
		}

		public ParameterAttributes Attributes
		{
			get
			{
				return myAttributes;
			}
		}

		public MetadataMethodInfo Method
		{
			get
			{
				return myMethod;
			}
		}

		public MetadataTypeInfo ParameterType
		{
			get
			{
				return myType;
			}
		}

		public string Name
		{
			get
			{
				return myName;
			}
		}

		public int Position
		{
			get
			{
				return myPosition;
			}
		}
	}
}
