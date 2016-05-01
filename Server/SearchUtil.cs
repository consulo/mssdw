using Microsoft.Samples.Debugging.CorMetadata;

namespace Consulo.Internal.Mssdw.Server
{
	public class SearchUtil
	{
		public static MetadataTypeInfo FindType(DebugSession debugSession, TypeRef typeRef)
		{
			CorMetadataImport metadataForModule = debugSession.GetMetadataForModule(typeRef.GetModuleName());
			if(metadataForModule != null)
			{
				return metadataForModule.CreateMetadataTypeInfo(typeRef);
			}
			return null;
		}

		public static MetadataFieldInfo FindField(DebugSession debugSession, TypeRef typeRef, int fieldToken)
		{
			MetadataTypeInfo type = FindType(debugSession, typeRef);
			if(type != null)
			{
				foreach (MetadataFieldInfo field in type.GetFields())
				{
					if(field.MetadataToken == fieldToken)
					{
						return field;
					}
				}
			}
			return null;
		}
	}
}