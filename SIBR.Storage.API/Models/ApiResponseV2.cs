using System.Collections.Generic;

namespace SIBR.Storage.API.Models
{
    public class ApiResponseV2<T>
    {
        public IEnumerable<T> Items { get; set; }
    }
}