#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System.Linq;
using Calamari.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using NUnit.Framework;
using Octostache;

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

        [Test]
        public void CanDeserializeNestedVariable()
        {
            var variables = new CalamariVariables();
            const string json = @"[
                {""Identity"": ""#{UserName}"", ""Access"": ""FullControl""}
            ]";
            variables.Set("UserName", "AmericanEagles\\RogerRamjet");
            variables.Set(SpecialVariables.Certificate.PrivateKeyAccessRules, json);

            var result = ImportCertificateCommand.GetPrivateKeyAccessRules(variables).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("AmericanEagles\\RogerRamjet", result[0].Identity.ToString());
            Assert.AreEqual(PrivateKeyAccess.FullControl, result[0].Access);
        }
    }
}
#endif