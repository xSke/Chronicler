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
    }

    public static class UpdateQueryExtensions
    {
        public static Query ApplyFrom(this Query q, IUpdateQueryOpts opts, string timestampColumn)
        {
            if (opts.Before != null)
                q.Where(timestampColumn, "<", opts.Before.Value);
            
            if (opts.After != null)
                q.Where(timestampColumn, ">", opts.After.Value);

            if (opts.Count != null)
                q.Limit(opts.Count.Value);

            if (opts.Reverse)
                q.OrderByDesc(timestampColumn);
            else
                q.OrderBy(timestampColumn);
            
            return q;
        }
    }
}