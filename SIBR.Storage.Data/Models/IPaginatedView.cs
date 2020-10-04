using SIBR.Storage.Data.Query;

namespace SIBR.Storage.Data.Models
{
    public interface IPaginatedView
    {
        public PageToken NextPage { get; }
    }
}