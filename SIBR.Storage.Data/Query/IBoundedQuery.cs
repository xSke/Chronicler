namespace SIBR.Storage.Data.Query
{
    public interface IBoundedQuery<T> where T: struct
    {
        public T? Before { get; }
        public T? After { get; }
    }
}