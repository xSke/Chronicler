namespace SIBR.Storage.Data.Query
{
    public interface IPaginatedQuery : ISortedQuery
    {
        public PageToken Page { get; }
    }
}