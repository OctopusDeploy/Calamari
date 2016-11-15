#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System.Linq;
using Calamari.Integration.Certificates;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    public class PrivateKeyAccessRuleSerializationTestFixture
    {
        [Test]
        public void CanDeserializeJson()
        {
            const string json = @"[
                {""Identity"": ""BUILTIN\\Administrators"", ""Access"": ""FullControl""},
                {""Identity"": ""BUILTIN\\Users"", ""Access"": ""ReadOnly""}
            ]";
            var result = PrivateKeyAccessRule.FromJson(json).ToList();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("BUILTIN\\Administrators", result[0].Identity.ToString());
            Assert.AreEqual(PrivateKeyAccess.FullControl, result[0].Access);
            Assert.AreEqual("BUILTIN\\Users", result[1].Identity.ToString());
            Assert.AreEqual(PrivateKeyAccess.ReadOnly, result[1].Access);
        }
    }
}
#endif