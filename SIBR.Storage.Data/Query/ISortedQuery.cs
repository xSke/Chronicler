namespace SIBR.Storage.Data.Query
{
    public interface ISortedQuery: IQuery
    {
        public SortOrder Order { get; }
    }
}