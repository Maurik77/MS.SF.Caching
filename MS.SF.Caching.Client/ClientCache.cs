using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.Services.ServiceFabric.ReliableServices.Caching.Interfaces;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Services.ServiceFabric.Caching
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="MS.SF.Caching.IClientCache" />
    public class ClientCache : IClientCache
    {
        /// <summary>
        /// The fabric client
        /// </summary>
        private static FabricClient fabricClient = new FabricClient();
        /// <summary>
        /// The number of partitions
        /// </summary>
        private int numberOfPartitions;
        /// <summary>
        /// The service URI
        /// </summary>
        private Uri serviceUri = new Uri("fabric:/MS.SF.Caching");
        /// <summary>
        /// The partition list
        /// </summary>
        private ServicePartitionList partitionList;
        /// <summary>
        /// The partition information list
        /// </summary>
        private List<Int64RangePartitionInformation> partitionInfoList;
        /// <summary>
        /// The current
        /// </summary>
        public static readonly ClientCache Current = new ClientCache();

        /// <summary>
        /// Prevents a default instance of the <see cref="ClientCache"/> class from being created.
        /// </summary>
        private ClientCache()
        {
            this.ReadSettings();
        }

        private void ReadSettings()
        {
            //TODO Leggere ulteriori configurazione dai settings sezione "MS.SF.Caching"
        }

        /// <summary>
        /// Gets the partitions information.
        /// </summary>
        /// <param name="serviceUri">The service URI.</param>
        /// <returns></returns>
        private async Task GetPartitionsInfo(Uri serviceUri)
        {
            this.partitionList = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);
            this.partitionInfoList = this.partitionList.Select(p => p.PartitionInformation as Int64RangePartitionInformation).ToList();
            this.numberOfPartitions = partitionList.Count;
        }

        /// <summary>
        /// Tries the get asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public async Task<T> TryGetAsync<T>(string key, string region = null)
        {
            var proxy = GetProxy(region, key);
            var result = await proxy.TryGetAsync(region, key);
            return (T)result;
        }

        /// <summary>
        /// Gets the or add asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="add">The add.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public async Task<T> GetOrAddAsync<T>(string key, Func<CacheItem<T>> add, string region = null)
        {
            var proxy = GetProxy(region, key);
            var result = await proxy.TryGetAsync(region, key);

            if (result == null)
            {
                var cacheItemInfo = add();
                await this.SetOrUpdateAsync(key, cacheItemInfo.Item, cacheItemInfo.Policy, region);
            }

            return (T)result;
        }

        /// <summary>
        /// Sets the or update asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="obj">The object.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public Task SetOrUpdateAsync<T>(string key, T obj, CacheItemPolicy policy, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.SetOrUpdateAsync(region, key, obj, policy);
        }

        /// <summary>
        /// Tries the update policy asynchronous.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="policy">The policy.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public Task<ExecutionResult> TryUpdatePolicyAsync(string key, CacheItemPolicy policy, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.TryUpdatePolicyAsync(region, key, policy);
        }

        /// <summary>
        /// Tries the delete asynchronous.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public Task<ExecutionResult> TryDeleteAsync(string key, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.TryDeleteAsync(region, key);
        }

        /// <summary>
        /// Cleans the asynchronous.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <returns></returns>
        public Task CleanAsync(string region)
        {
            var proxy = GetProxy(region);
            return proxy.CleanAsync(region);
        }

        /// <summary>
        /// Cleans all asynchronous.
        /// </summary>
        /// <returns></returns>
        public Task CleanAllAsync()
        {
            List<Task> alltask = new List<Task>();

            foreach (var item in this.partitionInfoList)
            {
                var proxy = ServiceProxy.Create<IDistribuitedCache>(this.serviceUri, new ServicePartitionKey(item.LowKey));
                alltask.Add(proxy.CleanAllAsync());
            }

            return Task.WhenAll(alltask.ToArray());
        }

        /// <summary>
        /// Gets the proxy.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        private IDistribuitedCache GetProxy(string region, string key = null)
        {
            var partitionKey = CalculatePartitionKey(region, key, this.numberOfPartitions);

            return ServiceProxy.Create<IDistribuitedCache>(this.serviceUri, partitionKey);
        }

        /// <summary>
        /// Calculates the partition key.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <param name="key">The key.</param>
        /// <param name="numberOfPartitions">The number of partitions.</param>
        /// <returns></returns>
        private ServicePartitionKey CalculatePartitionKey(string region, string key, int numberOfPartitions)
        {
            var fullKey = string.Concat(region, string.IsNullOrWhiteSpace(region) ? null : "_", key);
            return new ServicePartitionKey(fullKey.GetHashCode());
        }
    }
}
