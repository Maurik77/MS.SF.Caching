using System.Threading.Tasks;
using System;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;

namespace Microsoft.Services.ServiceFabric.Caching
{
    /// <summary>
    /// 
    /// </summary>
    public interface IClientCache
    {
        /// <summary>
        /// Cleans all asynchronous.
        /// </summary>
        /// <returns></returns>
        Task CleanAllAsync();
        /// <summary>
        /// Cleans the asynchronous.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task CleanAsync(string region);
        /// <summary>
        /// Tries the delete asynchronous.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task<ExecutionResult> TryDeleteAsync(string key, string region = null);
        /// <summary>
        /// Tries the get asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task<T> TryGetAsync<T>(string key, string region = null);
        /// <summary>
        /// Gets the or add asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="add">The add.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task<T> GetOrAddAsync<T>(string key, Func<CacheItem<T>> add, string region = null);
        /// <summary>
        /// Sets the or update asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="obj">The object.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task SetOrUpdateAsync<T>(string key, T obj, CacheItemPolicy policy, string region = null);
        /// <summary>
        /// Tries the update policy asynchronous.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        Task<ExecutionResult> TryUpdatePolicyAsync(string key, CacheItemPolicy policy, string region = null);
    }
}