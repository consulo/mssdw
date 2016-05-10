//
// MetadataExtensions.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//       Therzok <teromario@yahoo.com>
//
// Copyright (c) 2013 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Consulo.Internal.Mssdw;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Microsoft.Samples.Debugging.Extensions
{
	// [Xamarin] Expression evaluator.
	public static class MetadataExtensions
	{
		internal static bool TypeFlagsMatch(bool isPublic, bool isStatic, BindingFlags flags)
		{
			if(isPublic && (flags & BindingFlags.Public) == 0)
				return false;
			if(!isPublic && (flags & BindingFlags.NonPublic) == 0)
				return false;
			if(isStatic && (flags & BindingFlags.Static) == 0)
				return false;
			if(!isStatic && (flags & BindingFlags.Instance) == 0)
				return false;
			return true;
		}

		internal static MetadataTypeInfo MakeDelegate(MetadataTypeInfo retType, List<MetadataTypeInfo> argTypes)
		{
			throw new NotImplementedException();
		}

		public static MetadataTypeInfo MakeArray(MetadataTypeInfo t, List<int> sizes, List<int> loBounds)
		{
			var mt = t as MetadataTypeInfo;
			if(mt != null)
			{
				if(sizes == null)
				{
					sizes = new List<int>();
					sizes.Add(1);
				}
				mt.m_arraySizes = sizes;
				mt.m_arrayLoBounds = loBounds;
				return mt;
			}
			throw new NotImplementedException();
		}

		public static MetadataTypeInfo MakeByRef(MetadataTypeInfo t)
		{
			var mt = t as MetadataTypeInfo;
			if(mt != null)
			{
				mt.m_isByRef = true;
				return mt;
			}
			throw new NotImplementedException();
		}

		public static MetadataTypeInfo MakePointer(MetadataTypeInfo t)
		{
			var mt = t as MetadataTypeInfo;
			if(mt != null)
			{
				mt.m_isPtr = true;
				return mt;
			}
			throw new NotImplementedException();
		}

		public static MetadataTypeInfo MakeGeneric(MetadataTypeInfo t, List<MetadataTypeInfo> typeArgs)
		{
			t.m_typeArgs = typeArgs;
			return t;
		}
	}

	// [Xamarin] Expression evaluator.
	[CLSCompliant(false)]
	public static class MetadataHelperFunctionsExtensions
	{
		public static Dictionary<CorElType, Type> CoreTypes = new Dictionary<CorElType, Type>();

		static MetadataHelperFunctionsExtensions()
		{
			CoreTypes.Add(CorElType.ELEMENT_TYPE_BOOLEAN, typeof (bool));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_CHAR, typeof (char));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_I1, typeof (sbyte));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_U1, typeof (byte));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_I2, typeof (short));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_U2, typeof (ushort));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_I4, typeof (int));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_U4, typeof (uint));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_I8, typeof (long));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_U8, typeof (ulong));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_R4, typeof (float));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_R8, typeof (double));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_STRING, typeof (string));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_I, typeof (IntPtr));
			CoreTypes.Add(CorElType.ELEMENT_TYPE_U, typeof (UIntPtr));
		}

		internal static void ReadMethodSignature(CorMetadataImport corMetadataImport, IMetadataImport importer, ref IntPtr pData, out CorCallingConvention cconv, out MetadataTypeInfo retType, out List<MetadataTypeInfo> argTypes)
		{
			cconv = MetadataHelperFunctions.CorSigUncompressCallingConv(ref pData);
			uint numArgs = 0;
			// FIXME: Use number of <T>s.
			uint types = 0;
			if((cconv & CorCallingConvention.Generic) == CorCallingConvention.Generic)
				types = MetadataHelperFunctions.CorSigUncompressData(ref pData);

			if(cconv != CorCallingConvention.Field)
				numArgs = MetadataHelperFunctions.CorSigUncompressData(ref pData);

			retType = ReadType(corMetadataImport, importer, ref pData);
			argTypes = new List<MetadataTypeInfo>();
			for(int n = 0; n < numArgs; n++)
				argTypes.Add(ReadType(corMetadataImport, importer, ref pData));
		}

		public static MetadataTypeInfo ReadType(CorMetadataImport corMetadataImport, IMetadataImport importer, ref IntPtr pData)
		{
			CorElType et;
			unsafe
			{
				var pBytes = (byte*)pData;
				et = (CorElType) (*pBytes);
				pData = (IntPtr) (pBytes + 1);
			}

			switch(et)
			{
				case CorElType.ELEMENT_TYPE_VOID:
					return FixedType(corMetadataImport.DebugSession, typeof(void));
				case CorElType.ELEMENT_TYPE_BOOLEAN:
					return FixedType(corMetadataImport.DebugSession, typeof(bool));
				case CorElType.ELEMENT_TYPE_CHAR:
					return FixedType(corMetadataImport.DebugSession, typeof(char));
				case CorElType.ELEMENT_TYPE_I1:
					return FixedType(corMetadataImport.DebugSession, typeof(sbyte));
				case CorElType.ELEMENT_TYPE_U1:
					return FixedType(corMetadataImport.DebugSession, typeof(byte));
				case CorElType.ELEMENT_TYPE_I2:
					return FixedType(corMetadataImport.DebugSession, typeof(short));
				case CorElType.ELEMENT_TYPE_U2:
					return FixedType(corMetadataImport.DebugSession, typeof(ushort));
				case CorElType.ELEMENT_TYPE_I4:
					return FixedType(corMetadataImport.DebugSession, typeof(int));
				case CorElType.ELEMENT_TYPE_U4:
					return FixedType(corMetadataImport.DebugSession, typeof(uint));
				case CorElType.ELEMENT_TYPE_I8:
					return FixedType(corMetadataImport.DebugSession, typeof(long));
				case CorElType.ELEMENT_TYPE_U8:
					return FixedType(corMetadataImport.DebugSession, typeof(ulong));
				case CorElType.ELEMENT_TYPE_R4:
					return FixedType(corMetadataImport.DebugSession, typeof(float));
				case CorElType.ELEMENT_TYPE_R8:
					return FixedType(corMetadataImport.DebugSession, typeof(double));
				case CorElType.ELEMENT_TYPE_STRING:
					return FixedType(corMetadataImport.DebugSession, typeof(string));
				case CorElType.ELEMENT_TYPE_I:
					return FixedType(corMetadataImport.DebugSession, typeof(IntPtr));
				case CorElType.ELEMENT_TYPE_U:
					return FixedType(corMetadataImport.DebugSession, typeof(UIntPtr));
				case CorElType.ELEMENT_TYPE_OBJECT:
					return FixedType(corMetadataImport.DebugSession, typeof(object));
				case CorElType.ELEMENT_TYPE_VAR:
				case CorElType.ELEMENT_TYPE_MVAR:
					// Generic args in methods not supported. Return a dummy type.
					MetadataHelperFunctions.CorSigUncompressData(ref pData);
					return FixedType(corMetadataImport.DebugSession, typeof(object));

				case CorElType.ELEMENT_TYPE_GENERICINST:
				{
					MetadataTypeInfo t = ReadType(corMetadataImport, importer, ref pData);
					var typeArgs = new List<MetadataTypeInfo>();
					uint num = MetadataHelperFunctions.CorSigUncompressData(ref pData);
					for(int n = 0; n < num; n++)
					{
						typeArgs.Add(ReadType(corMetadataImport, importer, ref pData));
					}
					return MetadataExtensions.MakeGeneric(t, typeArgs);
				}

				case CorElType.ELEMENT_TYPE_PTR:
				{
					MetadataTypeInfo t = ReadType(corMetadataImport, importer, ref pData);
					return MetadataExtensions.MakePointer(t);
				}

				case CorElType.ELEMENT_TYPE_BYREF:
				{
					MetadataTypeInfo t = ReadType(corMetadataImport, importer, ref pData);
					return MetadataExtensions.MakeByRef(t);
				}

				case CorElType.ELEMENT_TYPE_END:
				case CorElType.ELEMENT_TYPE_VALUETYPE:
				case CorElType.ELEMENT_TYPE_CLASS:
				{
					uint token = MetadataHelperFunctions.CorSigUncompressToken(ref pData);
					return new MetadataTypeInfo(corMetadataImport, importer, (int) token);
				}

				case CorElType.ELEMENT_TYPE_ARRAY:
				{
					MetadataTypeInfo t = ReadType(corMetadataImport, importer, ref pData);
					int rank = (int)MetadataHelperFunctions.CorSigUncompressData(ref pData);
					if(rank == 0)
						return MetadataExtensions.MakeArray(t, null, null);

					uint numSizes = MetadataHelperFunctions.CorSigUncompressData(ref pData);
					var sizes = new List<int>(rank);
					for(int n = 0; n < numSizes && n < rank; n++)
						sizes.Add((int)MetadataHelperFunctions.CorSigUncompressData(ref pData));

					uint numLoBounds = MetadataHelperFunctions.CorSigUncompressData(ref pData);
					var loBounds = new List<int>(rank);
					for(int n = 0; n < numLoBounds && n < rank; n++)
						loBounds.Add((int)MetadataHelperFunctions.CorSigUncompressData(ref pData));

					return MetadataExtensions.MakeArray(t, sizes, loBounds);
				}

				case CorElType.ELEMENT_TYPE_SZARRAY:
				{
					MetadataTypeInfo t = ReadType(corMetadataImport, importer, ref pData);
					return MetadataExtensions.MakeArray(t, null, null);
				}

				case CorElType.ELEMENT_TYPE_FNPTR:
				{
					CorCallingConvention cconv;
					MetadataTypeInfo retType;
					List<MetadataTypeInfo> argTypes;
					ReadMethodSignature(corMetadataImport, importer, ref pData, out cconv, out retType, out argTypes);
					return MetadataExtensions.MakeDelegate(retType, argTypes);
				}

				case CorElType.ELEMENT_TYPE_CMOD_REQD:
				case CorElType.ELEMENT_TYPE_CMOD_OPT:
					return ReadType(corMetadataImport, importer, ref pData);
			}
			throw new NotSupportedException("Unknown sig element type: " + et);
		}

		public static MetadataTypeInfo FixedType(DebugSession debugSession, Type type)
		{
			CorMetadataImport module = debugSession.GetMSCorLibModule();
			Debug.Assert(module != null);
			int fromName = module.GetTypeTokenFromName(type.FullName);
			return new MetadataTypeInfo(module, module.m_importer, fromName);
		}

		static readonly object[] emptyAttributes = new object[0];

		static internal object[] GetDebugAttributes(IMetadataImport importer, int token)
		{
			var attributes = new ArrayList();
			object attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerTypeProxyAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerDisplayAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerBrowsableAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Runtime.CompilerServices.CompilerGeneratedAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerHiddenAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerStepThroughAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerNonUserCodeAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.Diagnostics.DebuggerStepperBoundaryAttribute));
			if(attr != null)
				attributes.Add(attr);
			attr = GetCustomAttribute(importer, token, typeof (System.FlagsAttribute));
			if(attr != null)
				attributes.Add(attr);
			return attributes.Count == 0 ? emptyAttributes : attributes.ToArray();
		}

		// [Xamarin] Expression evaluator.
		static internal object GetCustomAttribute(IMetadataImport importer, int token, Type type)
		{
			uint sigSize;
			IntPtr ppvSig;
			int hr = importer.GetCustomAttributeByName(token, type.FullName, out ppvSig, out sigSize);
			if(hr != 0)
				return null;

			var data = new byte[sigSize];
			Marshal.Copy(ppvSig, data, 0, (int)sigSize);
			var br = new BinaryReader(new MemoryStream(data));

			// Prolog
			if(br.ReadUInt16() != 1)
				throw new InvalidOperationException("Incorrect attribute prolog");

			ConstructorInfo ctor = type.GetConstructors()[0];
			ParameterInfo[] pars = ctor.GetParameters();

			var args = new object[pars.Length];

			// Fixed args
			for(int n = 0; n < pars.Length; n++)
				args[n] = ReadValue(br, pars[n].ParameterType);

			object ob = Activator.CreateInstance(type, args);

			// Named args
			uint nargs = br.ReadUInt16();
			for(; nargs > 0; nargs--)
			{
				byte fieldOrProp = br.ReadByte();
				byte atype = br.ReadByte();

				// Boxed primitive
				if(atype == 0x51)
					atype = br.ReadByte();
				var et = (CorElType) atype;
				string pname = br.ReadString();
				object val = ReadValue(br, CoreTypes[et]);

				if(fieldOrProp == 0x53)
				{
					FieldInfo fi = type.GetField(pname);
					fi.SetValue(ob, val);
				}
				else
				{
					PropertyInfo pi = type.GetProperty(pname);
					pi.SetValue(ob, val, null);
				}
			}
			return ob;
		}

		// [Xamarin] Expression evaluator.
		static object ReadValue(BinaryReader br, Type type)
		{
			if(type.IsEnum)
			{
				object ob = ReadValue(br, Enum.GetUnderlyingType(type));
				return Enum.ToObject(type, Convert.ToInt64(ob));
			}
			if(type == typeof (string) || type == typeof(Type))
				return br.ReadString();
			if(type == typeof (int))
				return br.ReadInt32();
			throw new InvalidOperationException("Can't parse value of type: " + type);
		}
	}
}

