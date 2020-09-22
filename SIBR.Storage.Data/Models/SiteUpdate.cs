using System;
using NodaTime;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class SiteUpdate
    {
        public Guid SourceId { get; set; }
        public Guid Hash { get; set; }
        public string Path { get; set; }
        public Instant Timestamp { get; set; }
        public byte[] Data { get; set; }

        public SiteUpdate(Guid sourceId, string path, Instant timestamp, byte[] data)
        {
            Path = path;
            Timestamp = timestamp;
            Hash = SibrHash.HashAsGuid(data);
            Data = data;
        }
    }
}