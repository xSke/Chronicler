using System;

namespace SIBR.Storage.Data.Models
{
    public interface IHashedObject<out T>
    {
        public Guid Hash { get; }
        public T Data { get; }
    }
}