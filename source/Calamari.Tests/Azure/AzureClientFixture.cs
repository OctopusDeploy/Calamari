using System;
using Calamari.Azure;
using Calamari.CloudAccounts;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Calamari.Tests.Azure
{
    public class AzureClientFixture
    {
        [Test]
        [TestCase("", "https://login.microsoftonline.com/")]
        [TestCase("AzureCloud", "https://login.microsoftonline.com/")]
        [TestCase("AzureGlobalCloud", "https://login.microsoftonline.com/")]
        [TestCase("AzureChinaCloud", "https://login.chinacloudapi.cn/")]
        [TestCase("AzureGermanCloud", "https://login.microsoftonline.de/")]
        [TestCase("AzureUSGovernment", "https://login.microsoftonline.us/")]
        public void AzureClientOptions_IdentityAuth_UsesCorrectEndpointsForRegions(string azureCloud, string expectedAuthorityHost)
        {
            // Arrange
            var servicePrincipalAccount = GetAccountFor(azureCloud);

            // Act
            var (_, tokenCredentialOptions) = servicePrincipalAccount.GetArmClientOptions();

            // Assert
            ClassicAssert.AreEqual(new Uri(expectedAuthorityHost), tokenCredentialOptions.AuthorityHost);
        }

        [Test]
        [TestCase("", "https://management.azure.com")]
        [TestCase("AzureCloud", "https://management.azure.com")]
        [TestCase("AzureGlobalCloud", "https://management.azure.com")]
        [TestCase("AzureChinaCloud", "https://management.chinacloudapi.cn")]
        [TestCase("AzureGermanCloud", "https://management.microsoftazure.de")]
        [TestCase("AzureUSGovernment", "https://management.usgovcloudapi.net")]
        public void AzureClientOptions_ApiCall_UsesCorrectEndpointsForRegions(string azureCloud, string expectedManagementEndpoint)
        {
            // Arrange
            var servicePrincipalAccount = GetAccountFor(azureCloud);

            // Act
            var (armClientOptions, _) = servicePrincipalAccount.GetArmClientOptions();

            // Assert
            ClassicAssert.AreEqual(new Uri(expectedManagementEndpoint), armClientOptions.Environment.Value.Endpoint);
        }

        private AzureServicePrincipalAccount GetAccountFor(string azureCloud)
        {
            return new AzureServicePrincipalAccount("123-456-789",
                                                    "clientId",
                                                    "tenantId",
                                                    "p@ssw0rd",
                                                    azureCloud,
                                                    null,
                                                    null);
        }
    }
}