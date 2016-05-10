/*
  Copyright (C) 2009 Volker Berlin (vberlin@inetsoftware.de)

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.

  Jeroen Frijters
  jeroen@frijters.net

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Consulo.Internal.Mssdw.Server;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata;

using CorElType = Microsoft.Samples.Debugging.CorDebug.NativeApi.CorElementType;

namespace Consulo.Internal.Mssdw.Network
{
	class Packet
	{
		private static int packetCounter;

		private const byte NoFlags = 0x0;
		private const byte Reply = 0x80;

		private byte[] data;
		private int offset;

		private int id;
		private byte cmdSet;
		private byte cmd;
		private short errorCode;
		private bool isEvent;

		private Stream output = new MemoryStream();

		/// <summary>
		/// Private constructor, use the factory methods
		/// </summary>
		private Packet()
		{
		}

		/// <summary>
		/// Create a packet from the stream.
		/// </summary>
		/// <param name="header">The first 11 bytes of the data.</param>
		/// <param name="stream">The stream with the data</param>
		/// <returns>a new Packet</returns>
		/// <exception cref="IOException">If the data in the stream are invalid.</exception>
		internal static Packet Read(byte[] header, Stream stream)
		{
			Packet packet = new Packet();
			packet.data = header;
			int len = packet.ReadInt();
			if(len < 11)
			{
				throw new IOException("protocol error - invalid length");
			}
			packet.id = packet.ReadInt();
			int flags = packet.ReadByte();
			if((flags & Reply) == 0)
			{
				packet.cmdSet = packet.ReadByte();
				packet.cmd = packet.ReadByte();
			}
			else
			{
				packet.errorCode = packet.ReadShort();
			}
			packet.data = new byte[len - 11];
			DebuggerUtil.ReadFully(stream, packet.data);
			packet.offset = 0;
			return packet;
		}

		/// <summary>
		/// Create a empty packet to send an Event from the target VM (debuggee) to the debugger.
		/// </summary>
		/// <returns>a new packet</returns>
		internal static Packet CreateEventPacket()
		{
			Packet packet = new Packet();
			packet.id = ++packetCounter;
			packet.cmdSet = (byte)Consulo.Internal.Mssdw.Network.CommandSet.Event;
			packet.cmd = 100;
			packet.isEvent = true;
			return packet;
		}

		/// <summary>
		/// Is used from JdwpConnection. You should use jdwpConnection.Send(Packet).
		/// </summary>
		/// <param name="stream"></param>
		internal void Send(Stream stream)
		{
			MemoryStream ms = (MemoryStream)output;
			try
			{
				output = stream;
				WriteInt((int)ms.Length + 11);
				WriteInt(id);
				if(!isEvent)
				{
					WriteByte(Reply);
					WriteShort(errorCode);
				}
				else
				{
					WriteByte(NoFlags);
					WriteByte(cmdSet);
					WriteByte(cmd);
				}
				ms.WriteTo(stream);
			}
			finally
			{
				output = ms; //remove the external stream
			}
		}

		internal int ReadInt()
		{
			return (data[offset++] << 24) |
			(data[offset++] << 16) |
			(data[offset++] << 8) |
			(data[offset++]);
		}

		internal short ReadShort()
		{
			return (short)((data[offset++] << 8) | (data[offset++]));
		}

		internal byte ReadByte()
		{
			return data[offset++];
		}

		internal bool ReadBool()
		{
			return data[offset++] != 0;
		}

		internal string ReadString()
		{
			System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
			int length = ReadInt();
			char[] chars = encoding.GetChars(data, offset, length);
			offset += length;
			return new String(chars);
		}

		internal TypeRef ReadTypeRef()
		{
			bool valid = ReadBool();
			if(!valid)
			{
				return null;
			}
			TypeRef typeRef = new TypeRef();
			typeRef.ModuleName = ReadString();
			typeRef.ClassToken = ReadInt();
			typeRef.IsPointer = ReadBool();
			typeRef.IsByRef = ReadBool();

			int arrayLenSize = ReadByte();
			if(arrayLenSize > 0)
			{
				typeRef.ArraySizes = new List<int>();
				for(int i = 0; i < arrayLenSize; i++)
				{
					typeRef.ArraySizes.Add(ReadInt());
				}
			}

			int arrayLowerBoundsSize = ReadByte();
			if(arrayLowerBoundsSize > 0)
			{
				typeRef.ArrayLowerBounds = new List<int>();
				for(int i = 0; i < arrayLowerBoundsSize; i++)
				{
					typeRef.ArrayLowerBounds.Add(ReadInt());
				}
			}

			return typeRef;
		}

		internal void WriteTypeRef(TypeRef typeRef)
		{
			WriteBool(typeRef != null);
			if(typeRef != null)
			{
				WriteString(typeRef.ModuleName);
				WriteInt(typeRef == null ? 0 : typeRef.ClassToken);
				WriteBool(typeRef != null && typeRef.IsPointer);
				WriteBool(typeRef != null && typeRef.IsByRef);
				if(typeRef != null && typeRef.ArraySizes != null)
				{
					WriteByte(typeRef.ArraySizes.Count);
					foreach (int arraySize in typeRef.ArraySizes)
					{
						WriteInt(arraySize);
					}
				}
				else
				{
					WriteByte(0);
				}

				if(typeRef != null && typeRef.ArrayLowerBounds != null)
				{
					WriteByte(typeRef.ArrayLowerBounds.Count);
					foreach (int arraySize in typeRef.ArrayLowerBounds)
					{
						WriteInt(arraySize);
					}
				}
				else
				{
					WriteByte(0);
				}
			}
		}

		internal CorValue ReadValue()
		{
			CorElType corElType = (CorElType) ReadByte();
			switch(corElType)
			{
				case CorElType.VALUE_TYPE_ID_NULL:
					return null;
				case CorElType.ELEMENT_TYPE_CLASS:
				case CorElType.ELEMENT_TYPE_OBJECT:
					int value = ReadInt();
					return CorValueRegistrator.Get(value);
				default:
					return null;
			}
		}

		internal void WriteValue(CorValue corValue, DebugSession debugSession)
		{
			if(corValue == null)
			{
				WriteByte((byte)CorElType.VALUE_TYPE_ID_NULL);
				return;
			}

			WriteValue(corValue.Id, corValue, debugSession);
		}

		private void WriteValue(int originalId, CorValue corValue, DebugSession debugSession)
		{
			CorReferenceValue toReferenceValue = corValue.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				if(toReferenceValue.IsNull)
				{
					WriteByte((byte)CorElType.VALUE_TYPE_ID_NULL);
					return;
				}

				WriteValue(originalId, toReferenceValue.Dereference(), debugSession);
				return;
			}

			CorElType corValueType = corValue.Type;
			WriteByte((int) corValueType);

			switch(corValueType)
			{
				case CorElType.ELEMENT_TYPE_CHAR:
				case CorElType.ELEMENT_TYPE_I:
				case CorElType.ELEMENT_TYPE_U:
				case CorElType.ELEMENT_TYPE_I1:
				case CorElType.ELEMENT_TYPE_U1:
				case CorElType.ELEMENT_TYPE_I2:
				case CorElType.ELEMENT_TYPE_U2:
				case CorElType.ELEMENT_TYPE_I4:
				case CorElType.ELEMENT_TYPE_U4:
					WriteInt((int) corValue.CastToGenericValue().GetValue());
					break;
				case CorElType.ELEMENT_TYPE_I8:
				case CorElType.ELEMENT_TYPE_U8:
					WriteLong((long) corValue.CastToGenericValue().GetValue());
					break;
				case CorElType.ELEMENT_TYPE_R4:
					WriteInt((int) BitConverter.DoubleToInt64Bits((float) corValue.CastToGenericValue().GetValue()));
					break;
				case CorElType.ELEMENT_TYPE_R8:
					WriteLong(BitConverter.DoubleToInt64Bits((double) corValue.CastToGenericValue().GetValue()));
					break;
				case CorElType.ELEMENT_TYPE_VOID:
					break;
				case CorElType.ELEMENT_TYPE_CLASS:
				{
					CorObjectValue objectValue = corValue.CastToObjectValue();
					WriteInt(originalId);
					WriteLong(objectValue.Address);
					WriteTypeRef(new TypeRef(objectValue.ExactType.GetTypeInfo(debugSession)));
					break;
				}
				case CorElType.ELEMENT_TYPE_VALUETYPE:
				{
					CorObjectValue objectValue = corValue.CastToObjectValue();
					WriteInt(originalId);
					WriteLong(objectValue.Address);
					MetadataTypeInfo metadataInfo = objectValue.ExactType.GetTypeInfo(debugSession);
					WriteTypeRef(new TypeRef(metadataInfo));
					WriteBool(metadataInfo.ReallyIsEnum);
					// we need skip static values
					MetadataFieldInfo[] fields = metadataInfo.GetFields().Where(field => (field.Attributes & FieldAttributes.Static) == 0).ToArray();
					WriteInt(fields.Length);
					foreach (MetadataFieldInfo field in fields)
					{
						CorValue value = objectValue.GetFieldValue(objectValue.Class, field.MetadataToken);
						WriteValue(value, debugSession);
					}
					break;
				}
				case CorElType.ELEMENT_TYPE_STRING:
					CorStringValue stringValue = corValue.CastToStringValue();
					WriteInt(originalId);
					WriteString(stringValue.String);
					break;
				case CorElType.ELEMENT_TYPE_BOOLEAN:
					WriteBool((bool) corValue.CastToGenericValue().GetValue());
					break;
				case CorElType.ELEMENT_TYPE_ARRAY:
				case CorElType.ELEMENT_TYPE_SZARRAY:
					CorArrayValue arrayValue = corValue.CastToArrayValue();
					WriteInt(originalId);
					WriteLong(arrayValue.Address);
					WriteTypeRef(new TypeRef(arrayValue.ExactType.GetTypeInfo(debugSession)));
					WriteInt(arrayValue.Count);
					break;
				default:
					Console.WriteLine("Unsupported corValue: {0:X}", corValueType);
					break;
			}
		}

		internal int Id
		{
			get
			{
				return id;
			}
		}

		internal int CommandSet
		{
			get
			{
				return cmdSet;
			}
		}

		internal int Command
		{
			get
			{
				return cmd;
			}
		}

		internal short Error
		{
			get
			{
				return errorCode;
			}
			set
			{
				errorCode = value;
			}
		}

		internal void WriteInt(int value)
		{
			output.WriteByte((byte)(value >> 24));
			output.WriteByte((byte)(value >> 16));
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value));
		}

		internal void WriteLong(long value)
		{
			output.WriteByte((byte)(value >> 56));
			output.WriteByte((byte)(value >> 48));
			output.WriteByte((byte)(value >> 40));
			output.WriteByte((byte)(value >> 32));
			output.WriteByte((byte)(value >> 24));
			output.WriteByte((byte)(value >> 16));
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value));
		}

		internal void WriteShort(int value)
		{
			output.WriteByte((byte)(value >> 8));
			output.WriteByte((byte)(value));
		}

		internal void WriteByte(int value)
		{
			output.WriteByte((byte)(value));
		}

		internal void WriteBool(bool value)
		{
			output.WriteByte(value ? (byte)1 : (byte)0);
		}

		internal void WriteString(string value)
		{
			System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
			byte[] bytes = encoding.GetBytes(value);
			WriteInt(bytes.Length);
			output.Write(bytes, 0, bytes.Length);
		}
	}
}