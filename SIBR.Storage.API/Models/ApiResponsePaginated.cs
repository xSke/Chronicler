using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Models
{
    public class ApiResponsePaginated<T>: ApiResponse<T>
    {
        public PageToken NextPage { get; set; }
    }
}