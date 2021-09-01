using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Azure.Accounts.Tests
{
    [TestFixture]
    public class AzureServicePrincipalAccountServiceMessageHandlerFixture
    {
        ICreateAccountDetailsServiceMessageHandler serviceMessageHandler;

        [OneTimeSetUp]
        public void SetUp()
        {
            serviceMessageHandler = new AzureServicePrincipalAccountServiceMessageHandler();
        }

        [Test]
        public void Ctor_Properties_ShouldBeInitializedCorrectly()
        {
            serviceMessageHandler.AuditEntryDescription.Should().Be("Azure Service Principal account");
            serviceMessageHandler.ServiceMessageName.Should().Be(AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.CreateAccountName);
        }

        [Test]
        public void CreateAccountDetails_WhenEnvironmentIsMissing_ShouldCreateDetailsCorrectly()
        {
            var properties = GetMessageProperties();
            properties.Remove(AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute);

            var details = serviceMessageHandler.CreateAccountDetails(properties, Substitute.For<ITaskLog>());

            AssertAzureServicePrincipalAccountDetails(details,
                                                      new ExpectedAccountDetailsValues
                                                      {
                                                          SubscriptionNumber = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute],
                                                          ClientId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute],
                                                          TenantId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute],
                                                          Password = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute].ToSensitiveString(),
                                                          AzureEnvironment = string.Empty,
                                                          ActiveDirectoryEndpointBaseUri = string.Empty,
                                                          ResourceManagementEndpointBaseUri = string.Empty
                                                      });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void CreateAccountDetails_WhenEnvironmentIsInvalid_ShouldCreateDetailsCorrectly(string environment)
        {
            var properties = GetMessageProperties();
            properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute] = environment;

            var details = serviceMessageHandler.CreateAccountDetails(properties, Substitute.For<ITaskLog>());

            AssertAzureServicePrincipalAccountDetails(details,
                                                      new ExpectedAccountDetailsValues
                                                      {
                                                          SubscriptionNumber = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute],
                                                          ClientId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute],
                                                          TenantId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute],
                                                          Password = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute].ToSensitiveString(),
                                                          AzureEnvironment = string.Empty,
                                                          ActiveDirectoryEndpointBaseUri = string.Empty,
                                                          ResourceManagementEndpointBaseUri = string.Empty
                                                      });
        }

        [Test]
        public void CreateAccountDetails_WhenEnvironmentIsNotMissingAndValid_ShouldCreateDetailsCorrectly()
        {
            var properties = GetMessageProperties();

            var details = serviceMessageHandler.CreateAccountDetails(properties, Substitute.For<ITaskLog>());

            AssertAzureServicePrincipalAccountDetails(details,
                                                      new ExpectedAccountDetailsValues
                                                      {
                                                          SubscriptionNumber = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute],
                                                          ClientId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute],
                                                          TenantId = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute],
                                                          Password = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute].ToSensitiveString(),
                                                          AzureEnvironment = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute],
                                                          ActiveDirectoryEndpointBaseUri = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute],
                                                          ResourceManagementEndpointBaseUri = properties[AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute]
                                                      });
        }

        static void AssertAzureServicePrincipalAccountDetails(AccountDetails accountDetails, ExpectedAccountDetailsValues expectedValues)
        {
            accountDetails.Should().BeOfType<AzureServicePrincipalAccountDetails>();
            var azureServicePrincipalAccountDetails = (AzureServicePrincipalAccountDetails)accountDetails;
            azureServicePrincipalAccountDetails.SubscriptionNumber.Should().Be(expectedValues.SubscriptionNumber);
            azureServicePrincipalAccountDetails.ClientId.Should().Be(expectedValues.ClientId);
            azureServicePrincipalAccountDetails.TenantId.Should().Be(expectedValues.TenantId);
            azureServicePrincipalAccountDetails.Password.Should().Be(expectedValues.Password);
            azureServicePrincipalAccountDetails.AzureEnvironment.Should().Be(expectedValues.AzureEnvironment);
            azureServicePrincipalAccountDetails.ResourceManagementEndpointBaseUri.Should().Be(expectedValues.ResourceManagementEndpointBaseUri);
            azureServicePrincipalAccountDetails.ActiveDirectoryEndpointBaseUri.Should().Be(expectedValues.ActiveDirectoryEndpointBaseUri);
        }

        static IDictionary<string, string> GetMessageProperties()
        {
            return new Dictionary<string, string>
            {
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute, "Subscription" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute, "Application" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute, "Password" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute, "Tenant" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute, "Test" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute, "https://localhost:6666" },
                { AzureServicePrincipalAccountServiceMessageHandler.CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute, "https://localhost:8888" }
            };
        }

        class ExpectedAccountDetailsValues
        {
            public string SubscriptionNumber { get; set; }

            public string ClientId { get; set; }

            public string TenantId { get; set; }

            public SensitiveString Password { get; set; }

            public string AzureEnvironment { get; set; }
            public string ResourceManagementEndpointBaseUri { get; set; }
            public string ActiveDirectoryEndpointBaseUri { get; set; }
        }
    }
}