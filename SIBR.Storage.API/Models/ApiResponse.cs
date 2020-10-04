using System.Collections.Generic;

namespace SIBR.Storage.API.Models
{
    public class ApiResponse<T>
    {
        public IEnumerable<T> Data { get; set; }
    }
}