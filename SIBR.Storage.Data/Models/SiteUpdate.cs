using System;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data.Models
{
    public class SiteUpdate
    {
        public Guid Hash { get; set; }
        public string Path { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public byte[] Data { get; set; }

        public SiteUpdate(string path, DateTimeOffset timestamp, byte[] data)
        {
            Path = path;
            Timestamp = timestamp;
            Hash = SibrHash.HashAsGuid(data);
            Data = data;
        }
    }
}