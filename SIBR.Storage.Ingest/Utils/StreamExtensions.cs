#nullable enable
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SIBR.Storage.Ingest.Utils
{
    public static class StreamExtensions
    {
        public static async Task<string?> ReadLineAsync(this StreamReader reader, CancellationToken ct)
        {
            var readTask = reader.ReadLineAsync();
            var delayTask = Task.Delay(-1, ct);

            var task = await Task.WhenAny(readTask, delayTask);
            if (task == delayTask)
                await delayTask;
            
            return await readTask;
        }
    }
}