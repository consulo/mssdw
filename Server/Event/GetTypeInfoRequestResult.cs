using Microsoft.Samples.Debugging.CorMetadata;
using System.Collections.Generic;
using System.Reflection;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetTypeInfoRequestResult
	{
		public class FieldInfo
		{
			public int Token;

			public string Name;

			/*public TypeRef Type;

			public int Attributes;  */
		}

		public class MethodInfo
		{
			public int Token;
		}

		public class PropertyInfo
		{
			public string Name;

		//	public TypeRef Type;

			/*public int Attributes;

			public int GetterToken;

			public int SetterToken;  */
		}

		public List<FieldInfo> Fields = new List<FieldInfo>();
		public List<PropertyInfo> Properties = new List<PropertyInfo>();
		public List<int> Methods = new List<int>();

		public bool IsArray;
		public string Name;
		public string FullName;
		public TypeRef BaseType;

		public void AddField(MetadataFieldInfo metadataFieldInfo)
		{
			FieldInfo fieldInfo = new FieldInfo();
			fieldInfo.Name = metadataFieldInfo.Name;
			//fieldInfo.Attributes = (int) metadataFieldInfo.Attributes;
			fieldInfo.Token = metadataFieldInfo.MetadataToken;
			//fieldInfo.Type = new TypeRef(metadataFieldInfo.FieldType);

			Fields.Add(fieldInfo);
		}

		public void AddMethod(MetadataMethodInfo metadataMethodInfo)
		{
		//	Methods.Add(metadataMethodInfo.MetadataToken);
		}

		public void AddProperty(MetadataPropertyInfo metadataFieldInfo)
		{
			PropertyInfo propertyInfo = new PropertyInfo();
			propertyInfo.Name = metadataFieldInfo.Name;
			//propertyInfo.Attributes = (int) metadataFieldInfo.Attributes;
			//propertyInfo.Type = new TypeRef(metadataFieldInfo.PropertyType);

		/*	MetadataMethodInfo getGetMethod = metadataFieldInfo.GetGetMethod();
			if(getGetMethod != null)
			{
				propertyInfo.GetterToken = getGetMethod.MetadataToken;
			}

			MetadataMethodInfo setGetMethod = metadataFieldInfo.GetSetMethod();
			if(setGetMethod != null)
			{
				propertyInfo.SetterToken = setGetMethod.MetadataToken;
			}    */
			Properties.Add(propertyInfo);
		}
	}
}