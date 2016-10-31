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
        object GetAsync(string region, string key);

        void SetOrUpdateAsync(string region, string key, string obj, Entities.CacheItemPolicy policy);

        void UpdatePolicyAsync(string region, string key, Entities.CacheItemPolicy policy);

        void DeleteAsync(string region, string key);

        void CleanAsync(string region);

        void CleanAllAsync();
    }
}
