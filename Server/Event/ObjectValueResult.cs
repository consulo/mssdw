using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorMetadata;
using System;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class ObjectValueResult
	{
		public int Id;
		public long Address;
		public int ObjectId;
		public TypeRef Type;

		public ObjectValueResult(CorValue original, DebugSession debugSession, CorObjectValue value)
		{
			Id = original == null ? -1 : original.Id;
			Address = original == null ? -1 : original.Address;
			ObjectId = value.Id;
			CorClass valueClass = value.Class;

			CorMetadataImport module = debugSession.GetMetadataForModule(valueClass.Module.Token);

			Type type = module.GetType(valueClass.Token);
			Type = new TypeRef(type);
		}
	}
}