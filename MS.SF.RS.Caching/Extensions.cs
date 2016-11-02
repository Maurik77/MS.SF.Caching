using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Services.ServiceFabric.ReliableServices.Caching
{
    public static class Extensions
    {
        public static Task<bool> ForEach<TKey, TValue>(this IReliableDictionary<TKey, TValue> reliableDictionary,
            ITransaction trx,
            Action<KeyValuePair<TKey, TValue>> iteration) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            var cancellationToken = new CancellationToken();
            return ForEach(reliableDictionary, trx, iteration, cancellationToken);
        }

        public static async Task<bool> ForEach<TKey, TValue>(
            IReliableDictionary<TKey, TValue> reliableDictionary, 
            ITransaction trx,
            Action<KeyValuePair<TKey, TValue>> iteration,
            CancellationToken cancellationToken) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            var enumerable = await reliableDictionary.CreateEnumerableAsync(trx);
            var enumerator = enumerable.GetAsyncEnumerator();

            while (await enumerator.MoveNextAsync(cancellationToken))
            {
                iteration(enumerator.Current);
            }

            return !cancellationToken.IsCancellationRequested;
        }
    }
}
