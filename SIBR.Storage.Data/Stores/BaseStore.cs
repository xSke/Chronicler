using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using NpgsqlTypes;
using SIBR.Storage.Data.Models;

namespace SIBR.Storage.Data
{
    public abstract class BaseStore
    {
        protected async Task<int> SaveObjects(NpgsqlConnection conn, IReadOnlyCollection<StoredObject> objects)
        {
            var hashesInDb = (await conn.QueryAsync<Guid>(
                "select hash from objects where hash = any(@Hashes)", new { Hashes = objects.Select(o => o.Hash).ToArray() }))
                .ToHashSet();


            var dictionary = new Dictionary<Guid, JToken>();
            foreach (var obj in objects) 
                dictionary[obj.Hash] = obj.Data;

            var hashes = new List<Guid>();
            var datas = new List<JToken>();
            foreach (var (hash, data) in dictionary)
            {
                if (hashesInDb.Contains(hash))
                    continue;
                
                hashes.Add(hash);
                datas.Add(data);
            }
            
            if (hashes.Count == 0)
                return 0;
            
            return await conn.ExecuteAsync("insert into objects select unnest(@Hashes), unnest(@Datas)::jsonb on conflict do nothing", new
            {
                Hashes = hashes, Datas = datas.Select(s => s.ToString(Formatting.None)).ToArray()
            });
        }
    }
}