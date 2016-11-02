using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Caching.Interfaces
{
    public interface IDistribuitedCache : IService
    {
        Task<object> TryGetAsync(string region, string key);

        Task SetOrUpdateAsync(string region, string key, object obj, CacheItemPolicy policy);

        Task<ExecutionResult> TryUpdatePolicyAsync(string region, string key, CacheItemPolicy policy);

        Task<ExecutionResult> TryDeleteAsync(string region, string key);

        Task CleanAsync(string region);

        Task CleanAllAsync();
    }
}
