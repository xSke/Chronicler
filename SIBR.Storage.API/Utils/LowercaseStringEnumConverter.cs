using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIBR.Storage.API.Utils
{
    public class LowercaseStringEnumConverter: JsonConverterFactory
    {
        private readonly JsonStringEnumConverter _inner = new JsonStringEnumConverter(new LowercaseNamingPolicy());

        public override bool CanConvert(Type typeToConvert) => _inner.CanConvert(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) => 
            _inner.CreateConverter(typeToConvert, options);
    }
}