using System;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities
{
    public class CacheItemPolicy
    {
        public DateTime AbsoluteExpiration { get; set; }

        public TimeSpan SlidingExpiration { get; set; }
    }
}