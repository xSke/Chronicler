#nullable enable
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Utils
{
    public static class KataQueryExtensions
    {
        public static SqlKata.Query ApplyBounds<T>(this SqlKata.Query q, IBoundedQuery<T> opts, string boundColumn) where T : struct
        {
            if (opts.Before != null)
                q.Where(boundColumn, "<", opts.Before.Value);
            if (opts.After != null)
                q.Where(boundColumn, ">", opts.After.Value);
            return q;
        }
        
        public static SqlKata.Query ApplySorting(this SqlKata.Query q, ISortedQuery opts, string timestampColumn,
            string entityIdColumn)
        {
            if (opts.Order == SortOrder.Asc)
                q.OrderBy(timestampColumn, entityIdColumn);
            else
                q.OrderByDesc(timestampColumn, entityIdColumn);

            if (opts is IPaginatedQuery pq)
            {
                if (pq.Page != null)
                    q.WhereRaw(opts.Order == SortOrder.Asc
                            ? $"({timestampColumn}, {entityIdColumn}) > (?, ?)"
                            : $"({timestampColumn}, {entityIdColumn}) < (?, ?)",
                        pq.Page.Timestamp, pq.Page.EntityId);
            }

            if (opts.Count != null)
                q.Limit(opts.Count.Value);
            
            return q;
        }
    }
}