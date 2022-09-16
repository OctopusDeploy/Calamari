using Calamari.Deployment.PackageRetention;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class CaseInsensitiveTinyTypeFixture
    {
        [Test]
        public void EquivalentValuesOfSameTypesAreEqual()
        {
            var tt1 = ATinyType.Create<ATinyType>("value");
            var tt2 = ATinyType.Create<ATinyType>("value");
            Assert.IsTrue(tt1.Equals(tt2));
        }

        [Test]
        public void EquivalentValuesOfDifferentTypesAreNotEqual()
        {
            var tt1 = ATinyType.Create<ATinyType>("value");
            var tt2 = AnotherTinyType.Create<AnotherTinyType>("value");
            Assert.IsFalse(tt1.Equals(tt2));
        }

        [Test]
        public void EquivalentValuesWithDifferentCasesOfSameTypesAreEqual()
        {
            var tt1 = ATinyType.Create<ATinyType>("VALUE");
            var tt2 = ATinyType.Create<ATinyType>("value");
            Assert.IsTrue(tt1.Equals(tt2));
        }

        [Test]
        public void EquivalentValuesWithDifferentCasesOfSameTypesHaveEqualHashCodes()
        {
            var tt1 = ATinyType.Create<ATinyType>("VALUE");
            var tt2 = ATinyType.Create<ATinyType>("value");
            Assert.AreEqual(tt1.GetHashCode(), tt2.GetHashCode());
        }

        [Test]
        public void EquivalentValuesOfDifferentTypesHaveUnequalHashCodes()
        {
            var tt1 = ATinyType.Create<ATinyType>("value");
            var tt2 = AnotherTinyType.Create<AnotherTinyType>("value");
            Assert.AreNotEqual(tt1.GetHashCode(), tt2.GetHashCode());
        }

        class ATinyType : CaseInsensitiveTinyType
        {
            public ATinyType(string value) : base(value)
            {
            }
        }

        class AnotherTinyType : CaseInsensitiveTinyType
        {
            public AnotherTinyType(string value) : base(value)
            {
            }
        }
    }
}