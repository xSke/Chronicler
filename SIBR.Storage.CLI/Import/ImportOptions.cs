using NodaTime;

namespace SIBR.Storage.CLI.Import
{
    public class ImportOptions
    {
        public string Directory { get; set; }
        public Instant? Before { get; set; }
        public Instant? After { get; set; }
    }
}