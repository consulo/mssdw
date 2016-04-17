using System;
using System.Reflection;
using Newtonsoft.Json;
using Consulo.Internal.Mssdw.Server.Event;

namespace Consulo.Internal.Mssdw.Server
{
	internal class ClientMessageConverter : JsonConverter
	{
		public override bool CanConvert(System.Type objectType)
		{
			return objectType == typeof(ClientMessage);
		}

		public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
		{
		}

		public override object ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
		{
			ClientMessage clientMessage = new ClientMessage();
			Type typeofClientMessage = typeof(ClientMessage);
			reader.Read();//skip start object
			while(reader.TokenType != JsonToken.EndObject)
			{
				if(reader.TokenType == JsonToken.PropertyName)
				{
					string propertyName = (string)reader.Value;

					FieldInfo field = typeofClientMessage.GetField(propertyName);

					reader.Read(); // skip property name

					if("Object" == propertyName)
					{
						string messageType = clientMessage.Type;

						Type objectValueType = Type.GetType("Consulo.Internal.Mssdw.Server.Request." + messageType);

						object objectValue = serializer.Deserialize(reader, objectValueType);

						field.SetValue(clientMessage, objectValue);
					}
					else
					{
						object deserialize = serializer.Deserialize(reader, field.FieldType);

						field.SetValue(clientMessage, deserialize);

						reader.Read(); // skip value
					}
				}
				else
				{
					Console.WriteLine("unknown:  " + reader.TokenType + ". " + reader.Value);
					break;
				}
			}

			reader.Read(); // eat end object
			return clientMessage;
		}
	}
}