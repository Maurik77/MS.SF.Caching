using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using MS.SF.RS.Caching.Interfaces;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MS.SF.Caching
{
    public class ClientCache : IClientCache
    {
        private static FabricClient fabricClient = new FabricClient();
        private int numberOfPartitions;
        private Uri serviceUri;
        private ServicePartitionList partitionList;
        private List<Int64RangePartitionInformation> partitionInfoList;
        public static readonly ClientCache Current = new ClientCache();

        private ClientCache()
        {

        }

        private async Task GetPartitionsInfo(Uri serviceUri)
        {
            this.partitionList = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);
            this.partitionInfoList = this.partitionList.Select(p => p.PartitionInformation as Int64RangePartitionInformation).ToList();
            this.numberOfPartitions = partitionList.Count;
        }

        public async Task<T> TryGetAsync<T>(string key, string region = null)
        {
            var proxy = GetProxy(region, key);
            var result = await proxy.TryGetAsync(region, key);
            return (T)result;
        }

        public async Task<T> GetOrAddAsync<T>(string key, Func<T> add, Entities.CacheItemPolicy policy, string region = null)
        {
            var proxy = GetProxy(region, key);
            var result = await proxy.TryGetAsync(region, key);

            if (result == null)
            {
                result = add();
                await this.SetOrUpdateAsync(key, result, policy, region);
            }

            return (T)result;
        }

        public Task SetOrUpdateAsync<T>(string key, T obj, Entities.CacheItemPolicy policy, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.SetOrUpdateAsync(region, key, obj, policy);
        }

        public Task<Entities.ExecutionResult> TryUpdatePolicyAsync(string key, Entities.CacheItemPolicy policy, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.TryUpdatePolicyAsync(region, key, policy);
        }

        public Task<Entities.ExecutionResult> TryDeleteAsync(string key, string region = null)
        {
            var proxy = GetProxy(region, key);
            return proxy.TryDeleteAsync(region, key);
        }

        public Task CleanAsync(string region)
        {
            var proxy = GetProxy(region);
            return proxy.CleanAsync(region);
        }

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

        private IDistribuitedCache GetProxy(string region, string key = null)
        {
            var partitionKey = CalculatePartitionKey(region, key, this.numberOfPartitions);

            return ServiceProxy.Create<IDistribuitedCache>(this.serviceUri, partitionKey);
        }

        private ServicePartitionKey CalculatePartitionKey(string region, string key, int numberOfPartitions)
        {
            throw new NotImplementedException();
        }
    }
}
