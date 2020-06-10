using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.AzureCloudService;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using NUnit.Framework;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureCloudService.Tests
{
    [TestFixture]
    public class AzureCloudServiceHealthCheckActionHandlerFixtures
    {
        [Test]
        public void Validate_Fails_If_Legacy_Account()
        {
            Action act = () => ActionHandlerTestBuilder.Create<AzureCloudServiceHealthCheckActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add(SpecialVariables.AccountType, "Boo");
                    context.Variables.Add(SpecialVariables.Action.Azure.AccountId, "myaccount");
                })
                .Execute();

            act.Should().Throw<KnownDeploymentFailureException>();
        }

        [Test]
        public void Validate_Fails_If_Wrong_Account_Type()
        {
            Action act = () => ActionHandlerTestBuilder.Create<AzureCloudServiceHealthCheckActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add(SpecialVariables.AccountType, "Boo");
                })
                .Execute();

            act.Should().Throw<KnownDeploymentFailureException>();
        }

        [Test]
        public async Task CloudService_Is_Found()
        {
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            var serviceName = $"{nameof(AzureCloudServiceHealthCheckActionHandlerFixtures)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";

            using var managementCertificate = CreateManagementCertificate(certificate);
            using var client =
                new ComputeManagementClient(new CertificateCloudCredentials(subscriptionId, managementCertificate));
            try
            {
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test"){ Location = "West US"});

                ActionHandlerTestBuilder.Create<AzureCloudServiceHealthCheckActionHandler, Program>()
                    .WithArrange(context =>
                    {
                        context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                        context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                        context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                        context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                        context.Variables.Add(SpecialVariables.AccountType, AccountTypes.AzureSubscriptionAccountType.ToString());
                    })
                    .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                    .Execute();
            }
            finally
            {
                client.HostedServices.DeleteAsync(serviceName).Ignore();
            }
        }

        [Test]
        public void CloudService_Is_Not_Found()
        {
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            var serviceName = $"{nameof(AzureCloudServiceHealthCheckActionHandlerFixtures)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";

            using var managementCertificate = CreateManagementCertificate(certificate);

            ActionHandlerTestBuilder.Create<AzureCloudServiceHealthCheckActionHandler, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                    context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                    context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                    context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                    context.Variables.Add(SpecialVariables.AccountType, AccountTypes.AzureSubscriptionAccountType.ToString());
                })
                .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                .Execute(false);
        }

        static X509Certificate2 CreateManagementCertificate(string certificate)
        {
            var bytes = Convert.FromBase64String(certificate);
            return new X509Certificate2(bytes);
        }
    }
}