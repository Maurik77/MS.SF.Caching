using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Services.ServiceFabric.ReliableServices.Interfaces.Entities;
using Microsoft.Services.ServiceFabric.Caching;
using System.Threading;

namespace UnitTest
{
    /// <summary>
    /// Summary description for LoadTest
    /// </summary>
    [TestClass]
    public class LoadTest
    {
        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void LoadTestNoRegion1()
        {
            CacheItemPolicy policy = new CacheItemPolicy(TimeSpan.FromMinutes(1));
            List<ComplexObject> objects = new List<ComplexObject>();
            Random rnd = new Random(3);

            for (int i = 0; i < 1000; i++)
            {
                objects.Add(ComplexObject.CreateWithList(Guid.NewGuid().ToString(), rnd.Next(20)));
            }

            foreach (var item in objects)
            {
                ClientCache.Current.SetOrUpdateAsync(item.Name, item, policy).GetAwaiter().GetResult();
            }

            Thread.Sleep(TimeSpan.FromSeconds(120));

            int z = 0;

            foreach (var item in objects)
            {
                var result = ClientCache.Current.TryGetAsync<ComplexObject>(item.Name).GetAwaiter().GetResult();

                Assert.IsNull(result, $"{z}-{item.Name}");
                z++;
            }
        }
    }
}
