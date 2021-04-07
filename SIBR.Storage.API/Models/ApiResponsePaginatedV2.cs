using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Models
{
    public class ApiResponsePaginatedV2<T>: ApiResponseV2<T>
    {
        public PageToken NextPage { get; set; }
    }
}