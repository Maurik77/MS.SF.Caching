﻿using System;
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
        private ConcurrentDictionary<string, CancellationTokenSource> garbageCancellationToken = new ConcurrentDictionary<string, CancellationTokenSource>();
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
            this.rootCancellationToken = cancellationToken;
            this.regionList = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, DateTime>>("RegionList");

            using (var trx = this.StateManager.CreateTransaction())
            {
                await this.regionList.ForEach(trx,
                    (item) =>
                    {
                        this.garbageCancellationToken.GetOrAdd(item.Key, new CancellationTokenSource());
                        this.GarbageExpiredItemsAsync(item.Key);
                    });
            }

            this.ReadSettings();
        }

        private void ReadSettings()
        {

        }

        private async Task GarbageExpiredItemsAsync(string region)
        {
            while (true)
            {
                if (this.rootCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                CancellationTokenSource cancellationTokenSource;
                if (!this.garbageCancellationToken.TryGetValue(region, out cancellationTokenSource))
                {
                    return;
                }

                var nextItemToRemove = await GetNextItemToRemove(region, cancellationTokenSource);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    continue;
                }

                //List is empty
                if (nextItemToRemove.Key == null)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationTokenSource.Token);
                    continue;
                }

                var delayFor = nextItemToRemove.Value.AbsoluteExpiration - DateTime.Now;

                await Task.Delay(delayFor, cancellationTokenSource.Token);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    continue;
                }

                if (this.rootCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await TryDeleteInternalAsync(region, nextItemToRemove.Key, true);
            }
        }

        private async Task<KeyValuePair<string, CacheItemPolicy>> GetNextItemToRemove(string region, CancellationTokenSource cancellationTokenSource)
        {
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

                if (forEachResult && nextItemToRemove.Key != null)
                {
                    await this.regionList.TryUpdateAsync(trx, region, nextItemToRemove.Value.AbsoluteExpiration, nextItemToRemove.Value.AbsoluteExpiration);
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
            //TODO 
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
            var dictionaryName = GetDictionaryName(region);

            using (var trx = this.StateManager.CreateTransaction())
            {
                if (!await this.regionList.ContainsKeyAsync(trx, dictionaryName))
                {
                    var currentMinDate = await this.regionList.GetOrAddAsync(trx, dictionaryName,
                        (k) =>
                        {
                            this.garbageCancellationToken.GetOrAdd(dictionaryName, new CancellationTokenSource());
                            this.GarbageExpiredItemsAsync(dictionaryName);
                            return policy.AbsoluteExpiration;
                        });

                    if (currentMinDate > policy.AbsoluteExpiration)
                    {
                        CancellationTokenSource garbageCancellationToken;
                        if (this.garbageCancellationToken.TryGetValue(dictionaryName, out garbageCancellationToken))
                        {
                            garbageCancellationToken.Cancel();
                            garbageCancellationToken.Dispose();
                            this.garbageCancellationToken.TryUpdate(dictionaryName, new CancellationTokenSource(), garbageCancellationToken);
                        }
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
            if (this.garbageCancellationToken.TryRemove(dictionaryName, out garbageCancellationToken))
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
