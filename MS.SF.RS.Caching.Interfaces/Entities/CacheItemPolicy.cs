using System;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities
{
    public class CacheItemPolicy
    {
        public DateTime AbsoluteExpiration { get; set; }

        //public TimeSpan SlidingExpiration { get; set; }

        public CacheItemPolicy()
        {

        }

        public CacheItemPolicy(TimeSpan duration)
        {
            this.AbsoluteExpiration = DateTime.Now + duration;
        }

        public CacheItemPolicy(DateTime absoluteExpiration)
        {
            this.AbsoluteExpiration = absoluteExpiration;
        }
    }
}