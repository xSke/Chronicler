using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIBR.Storage.Data.Query
{
    public class PageTokenJsonConverter: JsonConverter<PageToken>
    {
        public override PageToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str != null && PageToken.TryParse(str, out var token))
                return token;
            return null;
        }

        public override void Write(Utf8JsonWriter writer, PageToken value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Encode());
        }
    }
}