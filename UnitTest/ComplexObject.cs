using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    public class ComplexObject
    {
        public string Name { get; set; }

        public string StringProperty { get; set; }

        public int IntProperty { get; set; }

        public DateTime DateTimeProperty { get; set; }

        public bool BoolProperty { get; set; }

        public Guid GuidProperty { get; set; }

        public List<ComplexObject> List { get; set; } = new List<ComplexObject>();

        public static ComplexObject CreateSimple(string name)
        {
            var result = new ComplexObject
            {
                Name = name,
                BoolProperty = true,
                DateTimeProperty = DateTime.Now,
                IntProperty = (new Random()).Next(),
                StringProperty = Guid.NewGuid().ToString(),
                GuidProperty = Guid.NewGuid()
            };

            return result;
        }

        public static ComplexObject CreateWithList(string name, int listCount)
        {
            var result = CreateSimple(name);

            for (int i = 0; i < listCount; i++)
            {
                result.List.Add(CreateSimple($"{name}_{i}"));
            }

            return result;
        }
    }
}
