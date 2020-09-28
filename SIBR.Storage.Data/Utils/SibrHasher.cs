using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SIBR.Storage.Data.Utils
{
    public class SibrHasher: IDisposable
    {
        private readonly MemoryStream _memoryStream;
        private readonly StreamWriter _writer;
        private readonly SHA256 _hasher;

        public SibrHasher()
        {
            _memoryStream = new MemoryStream();
            _writer = new StreamWriter(_memoryStream, new UTF8Encoding(false));
            _hasher = SHA256.Create();
        }

        private void WriteTokenRecursive(JsonTextWriter writer, JToken elem)
        {
            switch (elem.Type)
            {
                case JTokenType.Object:
                    writer.WriteStartObject();
                    foreach (var prop in ((JObject) elem).Properties()
                        .OrderBy(prop => prop.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(prop.Name);
                        WriteTokenRecursive(writer, prop.Value);
                    }

                    writer.WriteEndObject();
                    break;
                case JTokenType.Array:
                    writer.WriteStartArray();
                    foreach (var token in (JArray) elem) 
                        WriteTokenRecursive(writer, token);

                    writer.WriteEndArray();
                    break;
                default:
                    elem.WriteTo(writer);
                    break;
            }
        }

        public Guid HashToken(JToken token)
        {
            _writer.Flush();
            _memoryStream.SetLength(0);

            using (var jsonWriter = new JsonTextWriter(_writer) { CloseOutput = false }) 
                WriteTokenRecursive(jsonWriter, token);
            _writer.Flush();
            
            var hash = _hasher.ComputeHash(_memoryStream.GetBuffer(), 0, (int) _memoryStream.Position); 
            return new Guid(hash.AsSpan(0, 16));
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _memoryStream?.Dispose();
        }
    }
}