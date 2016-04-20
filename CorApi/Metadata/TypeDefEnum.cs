//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
//
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections;
using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.CorApi.Metadata
{
	class TypeDefEnum : IEnumerable, IEnumerator, IDisposable
	{

		private CorMetadataImport m_corMeta;
		private IntPtr m_enum;
		private MetadataTypeInfo m_type;

		public TypeDefEnum(CorMetadataImport corMeta)
		{
			m_corMeta = corMeta;
		}

		~ TypeDefEnum()
		{
			DestroyEnum();
		}

		public void Dispose()
		{
			DestroyEnum();
			GC.SuppressFinalize(this);
		}

		//
		// IEnumerable interface
		//
		public IEnumerator GetEnumerator()
		{
			return this;
		}

		//
		// IEnumerator interface
		//
		public bool MoveNext()
		{
			int token;
			uint c;

			m_corMeta.m_importer.EnumTypeDefs(ref m_enum, out token, 1, out c);
			if(c == 1) // 1 new element
				m_type = m_corMeta.GetType(token);
			else
				m_type = null;
			return m_type != null;
		}

		public void Reset()
		{
			DestroyEnum();
			m_type = null;
		}

		public object Current
		{
			get
			{
				return m_type;
			}
		}

		protected void DestroyEnum()
		{
			m_corMeta.m_importer.CloseEnum(m_enum);
			m_enum = new IntPtr();
		}
	}
}