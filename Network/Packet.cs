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
using Consulo.Internal.Mssdw.Server;
using Consulo.Internal.Mssdw.Server.Event;
using Microsoft.Samples.Debugging.CorDebug;

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
			TypeRef typeRef = new TypeRef();
			typeRef.ModuleNameId = ReadInt();
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
			WriteInt(typeRef == null ? 0 : typeRef.ModuleNameId);
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

		internal void WriteValue(CorValue corValue)
		{
			if(corValue == null)
			{
				WriteByte((byte)CorElType.VALUE_TYPE_ID_NULL);
				return;
			}

			CorReferenceValue toReferenceValue = corValue.CastToReferenceValue();
			if(toReferenceValue != null)
			{
				if(toReferenceValue.IsNull)
				{
					WriteByte((byte)CorElType.VALUE_TYPE_ID_NULL);
					return;
				}

				WriteValue(toReferenceValue.Dereference());
				return;
			}

			CorElType corValueType = corValue.Type;
			WriteByte((int) corValueType);

			switch(corValueType)
			{
				/*case CorElType.ELEMENT_TYPE_CHAR:
					return new CharValueResult(originalValue, corValue.CastToGenericValue());
				case CorElType.ELEMENT_TYPE_I:
				case CorElType.ELEMENT_TYPE_U:
				case CorElType.ELEMENT_TYPE_I1:
				case CorElType.ELEMENT_TYPE_U1:
				case CorElType.ELEMENT_TYPE_I2:
				case CorElType.ELEMENT_TYPE_U2:
				case CorElType.ELEMENT_TYPE_I4:
				case CorElType.ELEMENT_TYPE_U4:
				case CorElType.ELEMENT_TYPE_I8:
				case CorElType.ELEMENT_TYPE_U8:
				case CorElType.ELEMENT_TYPE_R4:
				case CorElType.ELEMENT_TYPE_R8:
					return new NumberValueResult(originalValue, corValueType, corValue.CastToGenericValue());
				case CorElType.ELEMENT_TYPE_VOID:
					return new NullValueResult();
				case CorElType.ELEMENT_TYPE_CLASS:
				case CorElType.ELEMENT_TYPE_VALUETYPE:
					return new ObjectValueResult(originalValue, debugSession, corValue.CastToObjectValue());   */
				case CorElType.ELEMENT_TYPE_STRING:
					WriteString(corValue.CastToStringValue().String);
					break;
				case CorElType.ELEMENT_TYPE_BOOLEAN:
					WriteBool((bool) corValue.CastToGenericValue().GetValue());
					break;
				/*case CorElType.ELEMENT_TYPE_SZARRAY:
					return new ArrayValueResult(originalValue, debugSession, corValue.CastToArrayValue());
				default:
					return new UnknownValueResult("corValueType: " + string.Format("{0:X}", corValueType));  */
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