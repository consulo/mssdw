using System.Collections.Generic;

namespace Consulo.Internal.Mssdw.Server.Event
{
	public class GetMethodInfoRequestResult
	{
		public class ParameterInfo
		{
			public string Name;

			public TypeRef Type;
		}

		public string Name;

		public int Attributes;

		public List<ParameterInfo> Parameters = new List<ParameterInfo>();

		public void AddParameter(string name, TypeRef typeRef)
		{
			ParameterInfo parameterInfo = new ParameterInfo();
			parameterInfo.Name = name;
			parameterInfo.Type = typeRef;

			Parameters.Add(parameterInfo);
		}
	}
}