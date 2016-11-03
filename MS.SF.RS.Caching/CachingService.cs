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
using System.Collections.Concurrent;
using Microsoft.ServiceFabric.Data;
using System.Diagnostics;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Caching
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class CachingService : StatefulService, IDistribuitedCache
    {
        private const string UNREGION_ITEMS_DICTIONARY_NAME = "UNREGION_ITEMS";
        private const string ITEMS_PREFIX = "Items_";
        private const string POLICIES_PREFIX = "Policies_";

        private IReliableDictionary<string, DateTime> regionList;
        private ConcurrentDictionary<string, CancellationTokenSource> garbageCancellationTokenDictionary = new ConcurrentDictionary<string, CancellationTokenSource>();
        private CancellationToken rootCancellationToken;

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
            Debug.WriteLine($"CachingService::RunAsync(cancellationToken)");

            this.rootCancellationToken = cancellationToken;
            this.rootCancellationToken.Register(async () => await this.CleanUpResources());

            this.regionList = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, DateTime>>("RegionList");

            using (var trx = this.StateManager.CreateTransaction())
            {
                await this.regionList.ForEach(trx,
                    (item) =>
                    {
                        this.garbageCancellationTokenDictionary.GetOrAdd(item.Key, new CancellationTokenSource());
                        Task.Run(async () => await this.GarbageExpiredItemsAsync(item.Key));
                    });
            }

            this.ReadSettings();
        }

        private async Task CleanUpResources()
        {
            Debug.WriteLine($"CachingService::CleanUpResources");

            using (var trx = this.StateManager.CreateTransaction())
            {
                await this.regionList.ForEach(trx,
                    (item) =>
                    {
                        try
                        {
                            CancellationTokenSource cancellationToken;
                            if (this.garbageCancellationTokenDictionary.TryGetValue(item.Key, out cancellationToken))
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    cancellationToken.Cancel();
                                }
                                cancellationToken.Dispose();
                            }
                        }
                        catch { }
                    });
            }
        }

        private void ReadSettings()
        {

        }

        private async Task GarbageExpiredItemsAsync(string region)
        {
            Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})");

            while (true)
            {

                if (this.rootCancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> rootCancellationToken.IsCancellationRequested");
                    return;
                }

                CancellationTokenSource cancellationTokenSource;
                Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> retrieve CancellationTokenSource");
                if (!this.garbageCancellationTokenDictionary.TryGetValue(region, out cancellationTokenSource))
                {
                    Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> CancellationTokenSource not found");
                    return;
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> CancellationTokenSource IsCancellationRequested");
                    continue;
                }

                Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> Invokig GetNextItemToRemove");
                var nextItemToRemove = await GetNextItemToRemove(region);

                Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> GetNextItemToRemove: Key={nextItemToRemove.Key}, Value={nextItemToRemove.Value?.AbsoluteExpiration}");

                TimeSpan delayFor = Timeout.InfiniteTimeSpan;

                if (nextItemToRemove.Key != null)
                {
                    delayFor = nextItemToRemove.Value.AbsoluteExpiration - DateTime.Now;
                }

                if (delayFor == Timeout.InfiniteTimeSpan || delayFor.TotalSeconds > 0)
                {
                    try
                    {
                        Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> DelayFor: {delayFor}");
                        await Task.Delay(delayFor, cancellationTokenSource.Token);
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> Delay complete before timeout");
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            continue;
                        }
                    }
                }

                if (this.rootCancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> 2 rootCancellationToken.IsCancellationRequested");
                    return;
                }

                if (nextItemToRemove.Key != null)
                {
                    var deleteResult = await TryDeleteInternalAsync(region, nextItemToRemove.Key, true);
                    Debug.WriteLine($"CachingService::GarbageExpiredItemsAsync({region})--> TryDeleteInternalAsync({nextItemToRemove.Key}): result={deleteResult}");
                }
            }
        }

        private async Task<KeyValuePair<string, CacheItemPolicy>> GetNextItemToRemove(string region)
        {
            Debug.WriteLine($"CachingService::GetNextItemToRemove({region})");

            KeyValuePair<string, CacheItemPolicy> nextItemToRemove = new KeyValuePair<string, CacheItemPolicy>();
            var policiesDictionary = await GetReliableDictionaryAsync<CacheItemPolicy>(POLICIES_PREFIX, region);

            DateTime minDate = DateTime.MaxValue;

            using (var trx = this.StateManager.CreateTransaction())
            {
                var forEachResult = await policiesDictionary.ForEach(trx, (item) =>
                {
                    if (item.Value.AbsoluteExpiration < minDate)
                    {
                        nextItemToRemove = item;
                        minDate = nextItemToRemove.Value.AbsoluteExpiration;
                    }
                });

                Debug.WriteLine($"CachingService::GetNextItemToRemove({region})--> forEachResult={forEachResult}");
                if (forEachResult)
                {
                    //Se non ho trovato nulla imposto la data della region a MinValue così al prossimo inserimento viene aggiornata
                    Debug.WriteLine($"CachingService::GetNextItemToRemove({region})--> regionList.TryUpdateAsync({region}, {minDate})");
                    await this.regionList.AddOrUpdateAsync(trx, region, minDate, (k, oldValue) => minDate);
                    await trx.CommitAsync();
                }
            }

            return nextItemToRemove;
        }

        private async Task<ExecutionResult> TryDeleteInternalAsync(string region, string key, bool isInternal)
        {
            var result = ExecutionResult.NotFound;
            var itemsDictionary = await GetReliableDictionaryAsync<string>(ITEMS_PREFIX, region);
            var policiesDictionary = await GetReliableDictionaryAsync<CacheItemPolicy>(POLICIES_PREFIX, region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var removeItemResult = await itemsDictionary.TryRemoveAsync(trx, key);
                var removePolicyResult = await policiesDictionary.TryRemoveAsync(trx, key);

                if (removeItemResult.HasValue || removePolicyResult.HasValue)
                {
                    await trx.CommitAsync();
                    this.SendMetrics();
                    result = ExecutionResult.Done;
                }
            }

            return result;
        }

        private void SendMetrics()
        {
            //TODO Load Metrics
            //this.Partition.ReportLoad(new List<LoadMetric> { new LoadMetric("Memory", 1234), new LoadMetric("metric1", 42) });
        }

        private async Task<IReliableDictionary<string, TValue>> GetReliableDictionaryAsync<TValue>(string prefix, string region)
        {
            string dictionaryName = GetDictionaryName(region);

            dictionaryName = string.Concat(prefix, dictionaryName);

            return await this.StateManager.GetOrAddAsync<IReliableDictionary<string, TValue>>(dictionaryName);
        }

        private async Task RemoveReliableDictionaryAsync(string prefix, string region, ITransaction trx = null)
        {
            string dictionaryName = GetDictionaryName(region);

            dictionaryName = string.Concat(prefix, dictionaryName);

            if (trx != null)
            {
                await this.StateManager.RemoveAsync(trx, dictionaryName);
            }
            else
            {
                await this.StateManager.RemoveAsync(dictionaryName);
            }
        }

        private static string GetDictionaryName(string region)
        {
            var dictionaryName = UNREGION_ITEMS_DICTIONARY_NAME;

            if (!string.IsNullOrWhiteSpace(region))
            {
                dictionaryName = region.ToUpper();
            }

            return dictionaryName;
        }

        private async Task ManageGarbaging(string region, string key, CacheItemPolicy policy)
        {
            Debug.WriteLine($"CachingService::ManageGarbaging({region}, {key}, {policy.AbsoluteExpiration})");

            var dictionaryName = GetDictionaryName(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var currentMinDate = await this.regionList.GetOrAddAsync(trx, dictionaryName,
                    (k) =>
                    {
                        Debug.WriteLine($"CachingService::ManageGarbaging({region}, {key}, {policy.AbsoluteExpiration})--> Region is new");
                        this.garbageCancellationTokenDictionary.GetOrAdd(dictionaryName, new CancellationTokenSource());
                        Task.Run(async () => await this.GarbageExpiredItemsAsync(dictionaryName));
                        return policy.AbsoluteExpiration;
                    });

                Debug.WriteLine($"CachingService::ManageGarbaging({region}, {key}, {policy.AbsoluteExpiration})-->currentMinDate > policy.AbsoluteExpiration: {currentMinDate} > {policy.AbsoluteExpiration}");

                if (currentMinDate > policy.AbsoluteExpiration)
                {
                    CancellationTokenSource garbageCancellationTokenSource;
                    if (this.garbageCancellationTokenDictionary.TryGetValue(dictionaryName, out garbageCancellationTokenSource))
                    {
                        Debug.WriteLine($"{garbageCancellationTokenSource.GetHashCode()} -->garbageCancellationTokenSource.Cancel()");
                        this.garbageCancellationTokenDictionary.TryUpdate(dictionaryName, new CancellationTokenSource(), garbageCancellationTokenSource);
                        garbageCancellationTokenSource.Cancel();
                    }
                }

                await trx.CommitAsync();
            }
        }

        #region Service implementation
        public async Task CleanAllAsync()
        {

            //TODO Rimuovere tutti i dictionary
            //rimuovere le informazioni per garbage collector
            //rimuovere dalla lista le region

            using (var trx = this.StateManager.CreateTransaction())
            {
                var forEachResult = await this.regionList.ForEach(trx,
                    async (item) =>
                    {
                        if (item.Key != null)
                        {
                            await RemoveRegion(item.Key, trx);
                        }
                    });

                if (forEachResult)
                {
                    await trx.CommitAsync();
                }
            }

            //this.SendMetrics();
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

            using (var trx = this.StateManager.CreateTransaction())
            {
                await RemoveRegion(region, trx);
                await trx.CommitAsync();
            }

            //this.SendMetrics();
        }

        private async Task RemoveRegion(string region, ITransaction trx)
        {
            var dictionaryName = GetDictionaryName(region);

            await this.RemoveReliableDictionaryAsync(ITEMS_PREFIX, region, trx);
            await this.RemoveReliableDictionaryAsync(POLICIES_PREFIX, region, trx);
            await this.regionList.TryRemoveAsync(trx, dictionaryName);

            CancellationTokenSource garbageCancellationToken;
            if (this.garbageCancellationTokenDictionary.TryRemove(dictionaryName, out garbageCancellationToken))
            {
                garbageCancellationToken.Cancel();
            }
        }

        public async Task SetOrUpdateAsync(string region, string key, string serializedObj, CacheItemPolicy policy)
        {
            var itemsDictionary = await GetReliableDictionaryAsync<string>(ITEMS_PREFIX, region);
            var policiesDictionary = await GetReliableDictionaryAsync<CacheItemPolicy>(POLICIES_PREFIX, region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                await itemsDictionary.AddOrUpdateAsync(trx, key, serializedObj, (k, old) => serializedObj);
                await policiesDictionary.AddOrUpdateAsync(trx, key, policy, (k, old) => policy);
                await trx.CommitAsync();
            }

            await ManageGarbaging(region, key, policy);

            this.SendMetrics();
        }

        public async Task<ExecutionResult> TryDeleteAsync(string region, string key)
        {
            return await TryDeleteInternalAsync(region, key, false);
        }

        public async Task<string> TryGetAsync(string region, string key)
        {
            var dictionary = await GetReliableDictionaryAsync<string>(ITEMS_PREFIX, region);

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
            var dictionary = await GetReliableDictionaryAsync<CacheItemPolicy>(POLICIES_PREFIX, region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                var result = await dictionary.TryGetValueAsync(trx, key);
                if (result.HasValue)
                {
                    await dictionary.SetAsync(trx, key, result.Value);
                    await ManageGarbaging(region, key, policy);
                    await trx.CommitAsync();
                    return ExecutionResult.Done;
                }
                else
                {
                    return ExecutionResult.NotFound;
                }
            }
        }
        #endregion

    }
}
