using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Services.ServiceFabric.Caching;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;
using System.Threading;

namespace UnitTest
{
    [TestClass]
    public class WithRegionTest
    {
        const string REGION_NAME = "Region";
        const string OBJECT_KEY_SIMPLE = "ObjectKeySimple";
        const string OBJECT_KEY_COMPLEX = "ObjectKeyComplex";
        const string OBJECT_KEY_COMPLEX_WITHLIST = "ObjectKeyComplexWithList";

        #region Simple Object
        [TestMethod]
        public void AddSimpleObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_SIMPLE, "ValueInCache", new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetSimpleObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_SIMPLE, "ValueInCache", new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryGetAsync<string>(OBJECT_KEY_SIMPLE, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<string>("ValueInCache", result);
        }

        [TestMethod]
        public void RemoveSimpleObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_SIMPLE, "ValueInCache", new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryDeleteAsync(OBJECT_KEY_SIMPLE, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<ExecutionResult>(ExecutionResult.Done, result);
        }
        #endregion

        #region Complex Object
        [TestMethod]
        public void AddComplexObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX, ComplexObject.CreateSimple("ComplexObject"), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetComplexObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX, ComplexObject.CreateSimple("ComplexObject"), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryGetAsync<ComplexObject>(OBJECT_KEY_COMPLEX, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<string>("ComplexObject", result.Name);
        }

        [TestMethod]
        public void RemoveComplexObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX, ComplexObject.CreateSimple("ComplexObject"), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryDeleteAsync(OBJECT_KEY_COMPLEX, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<ExecutionResult>(ExecutionResult.Done, result);
        }
        #endregion

        #region Complex with List Object
        [TestMethod]
        public void AddComplexWithListObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX_WITHLIST, ComplexObject.CreateWithList("ComplexObjectWithList", 5), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetComplexWithListObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX_WITHLIST, ComplexObject.CreateWithList("ComplexObjectWithList", 5), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryGetAsync<ComplexObject>(OBJECT_KEY_COMPLEX_WITHLIST, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<string>("ComplexObjectWithList", result.Name);
        }

        [TestMethod]
        public void RemoveComplexWithListObject()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX_WITHLIST, ComplexObject.CreateWithList("ComplexObjectWithList", 5), new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            var result = ClientCache.Current.TryDeleteAsync(OBJECT_KEY_COMPLEX_WITHLIST, REGION_NAME).GetAwaiter().GetResult();

            Assert.AreEqual<ExecutionResult>(ExecutionResult.Done, result);
        }
        #endregion

        [TestMethod]
        public void CleanRegion()
        {
            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_SIMPLE, "ValueInCache", new CacheItemPolicy(TimeSpan.FromMinutes(1)), REGION_NAME).GetAwaiter().GetResult();

            ClientCache.Current.CleanAsync(REGION_NAME).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void AddComplexObjectWithExpriredTime()
        {
            var cacheItemPolicy = new CacheItemPolicy(TimeSpan.FromMinutes(1));
            ComplexObject result = null;

            ClientCache.Current.SetOrUpdateAsync(OBJECT_KEY_COMPLEX_WITHLIST, ComplexObject.CreateWithList("ComplexObjectWithList", 5), cacheItemPolicy, REGION_NAME).GetAwaiter().GetResult();

            while (cacheItemPolicy.AbsoluteExpiration > DateTime.Now)
            {
                result = ClientCache.Current.TryGetAsync<ComplexObject>(OBJECT_KEY_COMPLEX_WITHLIST, REGION_NAME).GetAwaiter().GetResult();

                Assert.AreEqual<string>("ComplexObjectWithList", result.Name);

                Thread.Sleep(5000);
            }

            result = ClientCache.Current.TryGetAsync<ComplexObject>(OBJECT_KEY_COMPLEX_WITHLIST, REGION_NAME).GetAwaiter().GetResult();
            Assert.IsNull(result);
        }
    }
}
