using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Management.Storage.Models;
using NUnit.Framework;

namespace Calamari.AzureCloudService.Tests
{
    public class DeployAzureCloudServiceCommandFixture
    {
        [Test]
        public async Task NewDeployment()
        {
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            var serviceName = $"{nameof(DeployAzureCloudServiceCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
            var storageName = $"Test{Guid.NewGuid().ToString("N").Substring(0, 10)}".ToLower();
            var deploymentSlot = DeploymentSlot.Staging;

            using var managementCertificate = CreateManagementCertificate(certificate);
            var subscriptionCloudCredentials = new CertificateCloudCredentials(subscriptionId, managementCertificate);
            using var storageClient = new StorageManagementClient(subscriptionCloudCredentials);
            using var client = new ComputeManagementClient(subscriptionCloudCredentials);
            try
            {
                await storageClient.StorageAccounts.CreateAsync(new StorageAccountCreateParameters(storageName, "test")
                {
                    Location = "West US",
                    AccountType = "Standard_LRS"
                });
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test") { Location = "West US" });
                await CommandTestBuilder.CreateAsync<DeployAzureCloudServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.StorageAccountName, storageName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.Slot, deploymentSlot.ToString());
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SwapIfPossible, bool.FalseString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.UseCurrentInstanceCount, bool.FalseString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.DeploymentLabel, "v1.0.0");

                                                         var pathToPackage = TestEnvironment.GetTestPath("Packages", "Octopus.Sample.AzureCloudService.6.0.0.nupkg");

                                                         context.WithPackage(pathToPackage);
                                                     })
                                        .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                        .Execute();
            }
            finally
            {
                await client.Deployments.DeleteBySlotAsync(serviceName, deploymentSlot);
                await client.HostedServices.DeleteAsync(serviceName);
                await storageClient.StorageAccounts.DeleteAsync(storageName);
            }
        }

        static X509Certificate2 CreateManagementCertificate(string certificate)
        {
            var bytes = Convert.FromBase64String(certificate);
            return new X509Certificate2(bytes);
        }
    }
}