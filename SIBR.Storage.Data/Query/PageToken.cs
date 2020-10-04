using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using NodaTime;
using SIBR.Storage.API.Utils;

namespace SIBR.Storage.Data.Query
{
    [TypeConverter(typeof(PageTokenTypeConverter))]
    [JsonConverter(typeof(PageTokenJsonConverter))]
    public class PageToken
    {
        private static readonly Instant Epoch =
            Instant.FromDateTimeOffset(new DateTimeOffset(2020, 7, 1, 0, 0, 0, TimeSpan.Zero));
        
        public Instant Timestamp { get; }
        public Guid EntityId { get; }

        public PageToken(Instant timestamp, Guid entityId)
        {
            Timestamp = timestamp;
            EntityId = entityId;
        }

        public string Encode()
        {
            var ticks = Timestamp.ToUnixTimeTicks() - Epoch.ToUnixTimeTicks();

            // bytes: [aaaaaaaaaaaaaaaabbbbbbbb], a = entity guid bytes, b = ticks since 2020-07-01Z, native endian
            var buf = new byte[24];
            
            EntityId.TryWriteBytes(buf.AsSpan(0, 16));
            BitConverter.TryWriteBytes(buf.AsSpan(16, 8), ticks);
            
            return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_');
        }

        public static bool TryParse(string input, out PageToken token)
        {
            token = default;

            var bytes = Convert.FromBase64String(input.Replace('-', '+').Replace('_', '/'));
            if (bytes.Length != 24)
                return false;
            
            var entityId = new Guid(bytes.AsSpan(0, 16));
            
            var ticks = BitConverter.ToInt64(bytes.AsSpan(16, 8));
            var timestamp = Instant.FromUnixTimeTicks(Epoch.ToUnixTimeTicks() + ticks);
            
            token = new PageToken(timestamp, entityId);
            return true;
        }
    }
}