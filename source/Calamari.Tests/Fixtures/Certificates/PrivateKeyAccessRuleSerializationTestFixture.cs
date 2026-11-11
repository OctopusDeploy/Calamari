using System.Linq;
using System.Runtime.Versioning;
using Calamari.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    [SupportedOSPlatform("windows")]
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

            ClassicAssert.AreEqual(2, result.Count);
            ClassicAssert.AreEqual("BUILTIN\\Administrators", result[0].Identity);
            ClassicAssert.AreEqual(PrivateKeyAccess.FullControl, result[0].Access);
            ClassicAssert.AreEqual("BUILTIN\\Users", result[1].Identity);
            ClassicAssert.AreEqual(PrivateKeyAccess.ReadOnly, result[1].Access);
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

            ClassicAssert.AreEqual(1, result.Count);
            ClassicAssert.AreEqual("AmericanEagles\\RogerRamjet", result[0].Identity);
            ClassicAssert.AreEqual(PrivateKeyAccess.FullControl, result[0].Access);
        }
    }
}
