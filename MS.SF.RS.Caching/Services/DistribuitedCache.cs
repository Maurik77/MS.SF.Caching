using Microsoft.Services.ServiceFabric.ReliableServices.Caching.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Caching.Services
{
    public class DistribuitedCache : IDistribuitedCache
    {
        public Task CleanAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task CleanAsync(string region)
        {
            throw new NotImplementedException();
        }

        public Task SetOrUpdateAsync(string region, string key, object obj, CacheItemPolicy policy)
        {
            throw new NotImplementedException();
        }

        public Task<ExecutionResult> TryDeleteAsync(string region, string key)
        {
            throw new NotImplementedException();
        }

        public Task<object> TryGetAsync(string region, string key)
        {
            throw new NotImplementedException();
        }

        public Task<ExecutionResult> TryUpdatePolicyAsync(string region, string key, CacheItemPolicy policy)
        {
            throw new NotImplementedException();
        }
    }
}
