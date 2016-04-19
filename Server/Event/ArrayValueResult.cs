using System;
using Microsoft.Samples.Debugging.CorMetadata;
using Microsoft.Samples.Debugging.CorDebug;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class ArrayValueResult
	{
		public int Id;

		public long Address;

		public int ObjectId;

		public int Length;

		public TypeRef Type;

		public ArrayValueResult(CorValue original, DebugSession debugSession, CorArrayValue value)
		{
			Id = original == null ? -1 : original.Id;
			Address = original == null ? -1 : original.Address;
			ObjectId = value.Id;
			Length = value.Count;

			/*CorClass valueClass = original.ExactType.Class;

			CorMetadataImport module = debugSession.GetMetadataForModule(valueClass.Module.Token);

			Type type = module.GetType(valueClass.Token);
			Type = new TypeRef(type);  */
		}
	}
}