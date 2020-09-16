using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Utils
{
    public class SibrHash
    {
        public static Guid HashAsGuid(byte[] dataToHash)
        {
            var hash = SHA256.Create().ComputeHash(dataToHash);
            return new Guid(hash.AsSpan(0, 16));
        }

        public static Guid HashAsGuid(JToken elem)
        {
            var hash = HashJson(elem);
            return new Guid(hash.AsSpan(0, 16));
        }

        private static void VisitWrite(JToken elem, JsonWriter writer)
        {
            switch (elem.Type)
            {
                case JTokenType.Object:
                    writer.WriteStartObject();
                    foreach (var prop in ((JObject) elem).Properties()
                        .OrderBy(prop => prop.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(prop.Name);
                        VisitWrite(prop.Value, writer);
                    }

                    writer.WriteEndObject();
                    break;
                case JTokenType.Array:
                    writer.WriteStartArray();
                    foreach (var token in (JArray) elem)
                    {
                        VisitWrite(token, writer);
                    }

                    writer.WriteEndArray();
                    break;
                default:
                    elem.WriteTo(writer);
                    break;
            }
        }
        public static byte[] HashJson(JToken elem)
        {
            using var hasher = SHA256.Create();

            using (var hashStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
            using (var textWriter = new StreamWriter(hashStream, new UTF8Encoding(false)))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                VisitWrite(elem, jsonWriter);
            }

            return hasher.Hash;
        }
    }
}