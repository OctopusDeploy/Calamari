using Calamari.Deployment.PackageRetention;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class TinyTypeFixture
    {
        [Test]
        public void EquivalentValuesWithSameTypesAreEqualTinyTypes()
        {
            var tt1 = ATinyType.Create<ATinyType>(10);
            var tt2 = ATinyType.Create<ATinyType>(10);
            Assert.AreEqual(tt1, tt2);
        }

        [Test]
        public void EquivalentValuesWithDifferentTypesAreNotEqualTinyTypes()
        {
            var tt1 = ATinyType.Create<ATinyType>(10);
            var tt2 = AnotherTinyType.Create<AnotherTinyType>(10);
            Assert.AreNotEqual(tt1, tt2);
        }

        [Test]
        public void EquivalentValuesWithSameTypesHaveEqualHashCodes()
        {
            var tt1 = ATinyType.Create<ATinyType>(10);
            var tt2 = ATinyType.Create<ATinyType>(10);
            Assert.AreEqual(tt1.GetHashCode(), tt2.GetHashCode());
        }

        [Test]
        public void EquivalentValuesWithDifferentTypesHaveUnequalHashCodes()
        {
            var tt1 = ATinyType.Create<ATinyType>(10);
            var tt2 = AnotherTinyType.Create<AnotherTinyType>(10);
            Assert.AreNotEqual(tt1.GetHashCode(), tt2.GetHashCode());
        }

        class ATinyType : TinyType<int>
        {
            public ATinyType(int value) : base(value)
            {
            }
        }

        class AnotherTinyType : TinyType<int>
        {
            public AnotherTinyType(int value) : base(value)
            {
            }
        }
    }
}