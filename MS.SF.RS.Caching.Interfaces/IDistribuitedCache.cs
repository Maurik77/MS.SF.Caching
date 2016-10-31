using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MS.SF.RS.Caching.Interfaces
{
    public interface IDistribuitedCache : IService
    {
        Task<object> TryGetAsync(string region, string key);

        Task SetOrUpdateAsync(string region, string key, object obj, Entities.CacheItemPolicy policy);

        Task<Entities.ExecutionResult> TryUpdatePolicyAsync(string region, string key, Entities.CacheItemPolicy policy);

        Task<Entities.ExecutionResult> TryDeleteAsync(string region, string key);

        Task CleanAsync(string region);

        Task CleanAllAsync();
    }
}
