using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.Services.ServiceFabric.ReliableServices.Caching.Interfaces;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Caching
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class CachingService : StatefulService, IDistribuitedCache
    {
        private const string UNREGION_ITEMS_DICTIONARY_NAME = "UnregionItems";

        private IReliableDictionary<string, string> regionList;
        private IReliableDictionary<DateTime, string> expiredItemsQueue;

        public CachingService(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[]
                {
                    new ServiceReplicaListener(context=>
                     this.CreateServiceRemotingListener(context))
                };
        }


        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            this.regionList = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>("RegionList");
            this.expiredItemsQueue = await this.StateManager.GetOrAddAsync<IReliableDictionary<DateTime, string>>("ExpiredItemsQueue");

            this.ReadSettings();
            await GarbageExpiredItemsAsync(cancellationToken);
        }

        private void ReadSettings()
        {
            throw new NotImplementedException();
        }

        private Task GarbageExpiredItemsAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        #region Service implementation
        public Task CleanAllAsync()
        {
            throw new NotImplementedException();
        }

        public async Task CleanAsync(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new ArgumentNullException(nameof(region));
            }

            //TODO Rimuovere il dictionary corrispondente
            //rimuovere le informazioni per garbage collector
            //rimuovere dalla lista delle region

            return;
        }

        public async Task SetOrUpdateAsync(string region, string key, object obj, CacheItemPolicy policy)
        {
            var dictionary = await GetReliableDictionary(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var cacheItem = new CacheItem<Object> { Item = obj, Policy = policy };
                await dictionary.AddOrUpdateAsync(trx, key, cacheItem, (k, old) => cacheItem);
            }
        }

        public async Task<ExecutionResult> TryDeleteAsync(string region, string key)
        {
            var dictionary = await GetReliableDictionary(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var result = await dictionary.TryRemoveAsync(trx, key);

                if (result.HasValue)
                {
                    return ExecutionResult.Done;
                }
                else
                {
                    return ExecutionResult.NotFound;
                }
            }
        }

        public async Task<object> TryGetAsync(string region, string key)
        {
            var dictionary = await GetReliableDictionary(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var result = await dictionary.TryGetValueAsync(trx, key);
                if (result.HasValue)
                {
                    return result.Value;
                }
                else
                {
                    return null;
                }
            }
        }

        public async Task<ExecutionResult> TryUpdatePolicyAsync(string region, string key, CacheItemPolicy policy)
        {
            var dictionary = await GetReliableDictionary(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var result = await dictionary.TryGetValueAsync(trx, key);
                if (result.HasValue)
                {
                    result.Value.Policy = policy;
                    await dictionary.SetAsync(trx, key, result.Value);
                    return ExecutionResult.Done;
                }
                else
                {
                    return ExecutionResult.NotFound;
                }
            }
        }
        #endregion

        private async Task<IReliableDictionary<string, CacheItem<object>>> GetReliableDictionary(string region)
        {
            var dictionaryName = UNREGION_ITEMS_DICTIONARY_NAME;
            region = region.ToUpper();

            if (!string.IsNullOrWhiteSpace(region))
            {
                dictionaryName = region;

                using (var trx = this.StateManager.CreateTransaction())
                {
                    if (!await this.regionList.ContainsKeyAsync(trx, region))
                    {
                        await this.regionList.AddOrUpdateAsync(trx, region, region, (key, old) => region);
                    }
                }
            }

            return await this.StateManager.GetOrAddAsync<IReliableDictionary<string, CacheItem<object>>>(dictionaryName);
        }

    }
}
