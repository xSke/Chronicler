using System;
using NodaTime;
using SqlKata;

namespace SIBR.Storage.Data.Utils
{
    public interface IUpdateQueryOpts
    {
        public Instant? Before { get; set; }
        public Instant? After { get; set; }
        public int? Count { get; set; }
        public bool Reverse { get; set; }
        public Guid? PageUpdateId { get; set; }
    }

    public static class UpdateQueryExtensions
    {
        public static Query ApplyFrom(this Query q, IUpdateQueryOpts opts, string timestampColumn, string table)
        {
            if (opts.Before != null)
                q.Where(timestampColumn, "<", opts.Before.Value);
            
            if (opts.After != null)
                q.Where(timestampColumn, ">", opts.After.Value);

            if (opts.Count != null)
                q.Limit(opts.Count.Value);

            if (opts.Reverse)
                q.OrderByDesc(timestampColumn, "update_id");
            else
                q.OrderBy(timestampColumn, "update_id");
            
            if (opts.PageUpdateId != null)
                q.WhereRaw(opts.Reverse
                        ? $"({timestampColumn}, update_id) < (select {timestampColumn}, update_id from {table} where update_id = ?)"
                        : $"({timestampColumn}, update_id) > (select {timestampColumn}, update_id from {table} where update_id = ?)", 
                    opts.PageUpdateId.Value);
            
            return q;
        }
    }
}