using System.Collections.Generic;
using SIBR.Storage.Data.Models;
using SIBR.Storage.Data.Utils;

namespace SIBR.Storage.Data
{
    public class TimeStore
    {
        private readonly Database _db;

        public TimeStore(Database db)
        {
            _db = db;
        }

        public IAsyncEnumerable<TimeMapEntry> GetTimeMap() => 
            _db.QueryStreamAsync<TimeMapEntry>("select * from time_map_view order by start_time", new object());
    }
}