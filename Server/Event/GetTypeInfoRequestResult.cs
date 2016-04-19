using Microsoft.Samples.Debugging.CorMetadata;
using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetTypeInfoRequestResult
	{
		public class FieldInfo
		{
			public int Token;

			public string Name;

			public TypeRef Type;

			public int Attributes;
		}

		public List<FieldInfo> Fields = new List<FieldInfo>();
		public bool isArray;
		public string Name;

		public void AddField(MetadataFieldInfo metadataFieldInfo)
		{
			FieldInfo fieldInfo = new FieldInfo();
			fieldInfo.Name = metadataFieldInfo.Name;
			fieldInfo.Attributes = (int) metadataFieldInfo.Attributes;
			fieldInfo.Token = metadataFieldInfo.MetadataToken;
			fieldInfo.Type = new TypeRef(metadataFieldInfo.FieldType);

			Fields.Add(fieldInfo);
		}
	}
}