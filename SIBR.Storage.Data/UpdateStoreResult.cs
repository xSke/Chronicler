namespace SIBR.Storage.Data
{
    public class UpdateStoreResult
    {
        public int NewUpdates { get; set; }
        public int NewObjects { get; set; }
        public int NewKeys { get; set; }

        public UpdateStoreResult(int newUpdates = 0, int newObjects = 0, int newKeys = 0)
        {
            NewUpdates = newUpdates;
            NewObjects = newObjects;
            NewKeys = newKeys;
        }
    }
}