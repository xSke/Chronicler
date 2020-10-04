using System;
using NodaTime;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class SiteUpdate: IHashedObject<byte[]>
    {
        public Guid SourceId { get; set; }
        public Guid Hash { get; set; }
        public string Path { get; set; }
        public Instant Timestamp { get; set; }
        public byte[] Data { get; set; }
        public Instant? LastModified { get; set; }

        public SiteUpdate()
        {
        }

        public static SiteUpdate From(Guid sourceId, string path, Instant timestamp, byte[] data, Instant? lastModified)
        {
            return new SiteUpdate
            {
                SourceId = sourceId,
                Path = path,
                Timestamp = timestamp,
                Hash = SibrHash.HashAsGuid(data),
                Data = data,
                LastModified = lastModified
            };
        }
    }
}