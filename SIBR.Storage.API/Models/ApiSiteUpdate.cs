using System;
using System.Linq;
using NodaTime;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.API.Models
{
    public class ApiSiteUpdate
    {
        public Instant Timestamp { get; set; }
        public string Path { get; set; }
        public Guid Hash { get; set; }
        public int Size { get; set; }
        public string DownloadUrl { get; set; }

        public ApiSiteUpdate(SiteUpdateView db)
        {
            var filename = db.Path.Split("/").Last();
            if (string.IsNullOrWhiteSpace(filename))
                filename = "index.html";
            
            Timestamp = db.Timestamp;
            Path = db.Path;
            Hash = db.Hash;
            Size = db.Size;
            DownloadUrl = $"/site/download/{Hash}/{filename}";
        }
    }
}