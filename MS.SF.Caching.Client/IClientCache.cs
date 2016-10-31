using System.Threading.Tasks;
using Entities;
using System;

namespace MS.SF.Caching
{
    public interface IClientCache
    {
        Task CleanAllAsync();
        Task CleanAsync(string region);
        Task<Entities.ExecutionResult> TryDeleteAsync(string key, string region = null);
        Task<T> TryGetAsync<T>(string key, string region = null);
        Task<T> GetOrAddAsync<T>(string key, Func<T> add, Entities.CacheItemPolicy policy, string region = null);
        Task SetOrUpdateAsync<T>(string key, T obj, CacheItemPolicy policy, string region = null);
        Task<Entities.ExecutionResult> TryUpdatePolicyAsync(string key, CacheItemPolicy policy, string region = null);
    }
}