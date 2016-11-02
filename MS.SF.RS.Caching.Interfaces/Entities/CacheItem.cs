namespace Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities
{
    public class CacheItem<T>
    {
        public T Item { get; set; }

        public CacheItemPolicy Policy { get; set; }
    }
}