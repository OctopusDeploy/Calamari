using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Testing;
using FluentAssertions;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using NUnit.Framework;

namespace Calamari.AzureCloudService.Tests
{
    public class HealthCheckCommandFixture
    {
        [Test]
        public async Task CloudService_Is_Found()
        {
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            var serviceName = $"{nameof(HealthCheckCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";

            using var managementCertificate = CreateManagementCertificate(certificate);
            using var client = new ComputeManagementClient(new CertificateCloudCredentials(subscriptionId, managementCertificate));
            try
            {
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test") { Location = "West US" });

                await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                                                     })
                                        .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                        .Execute();
            }
            finally
            {
                await client.HostedServices.DeleteAsync(serviceName);
            }
        }

        [Test]
        public Task CloudService_Is_Not_Found()
        {
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            var serviceName = $"{nameof(HealthCheckCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";

            using var managementCertificate = CreateManagementCertificate(certificate);

            return CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                                                      context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                                                      context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                                                      context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
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