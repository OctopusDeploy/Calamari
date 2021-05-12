using System;
using System.Text;
using Google;
using NUnit.Framework;
using Octopus.Data.Model;

namespace Sashimi.GoogleCloud.Accounts.Tests
{
    [TestFixture]
    public class GoogleCloudAccountVerifierFixtures
    {
        private static readonly string JsonEnvironmentVariableKey = "GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY";
        [TestCase]
        public void Verify_CredentialIsValid()
        {
            var environmentJsonKey = Environment.GetEnvironmentVariable(JsonEnvironmentVariableKey);
            if (environmentJsonKey == null)
            {
                throw new Exception($"Environment Variable `{JsonEnvironmentVariableKey}` could not be found. The value can be found in the password store under GoogleCloud - OctopusAPITester");
            }
            var jsonKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(environmentJsonKey));
            
            var verifier = new GoogleCloudAccountVerifier();
            var account = new GoogleCloudAccountDetails
            {
                JsonKey = jsonKey.ToSensitiveString()
            };
            Assert.DoesNotThrow(() => verifier.Verify(account));
        }
    }
}