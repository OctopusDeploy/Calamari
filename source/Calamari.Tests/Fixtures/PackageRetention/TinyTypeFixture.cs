using Calamari.Deployment.PackageRetention;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class TinyTypeFixture
    {
        [Test]
        public void EquivalentValuesAreEqualTinyTypes()
        {
            var tt1 = ATinyType.Create<ATinyType>("Value");
            var tt2 = AnotherTinyType.Create<AnotherTinyType>("Value");
            Assert.AreEqual(tt1, tt2);
        }

        [Test]
        public void EquivalentValuesHaveEqualHashCodes()
        {
            var tt1 = ATinyType.Create<ATinyType>("Value");
            var tt2 = AnotherTinyType.Create<AnotherTinyType>("Value");
            Assert.AreEqual(tt1.GetHashCode(), tt2.GetHashCode());
        }

        class ATinyType : TinyType<string>
        {
            public ATinyType(string value) : base(value)
            {
            }
        }

        class AnotherTinyType : TinyType<string>
        {
            public AnotherTinyType(string value) : base(value)
            {
            }
        }
    }
}