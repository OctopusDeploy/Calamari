using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Hyak.Common;
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
        string storageName;
        StorageManagementClient storageClient;
        CertificateCloudCredentials subscriptionCloudCredentials;
        X509Certificate2 managementCertificate;
        string subscriptionId;
        string certificate;
        string pathToPackage;

        [OneTimeSetUp]
        public async Task Setup()
        {
            storageName = $"Test{Guid.NewGuid().ToString("N").Substring(0, 10)}".ToLower();
            certificate = ExternalVariables.Get(ExternalVariable.AzureSubscriptionCertificate);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            managementCertificate = CreateManagementCertificate(certificate);
            subscriptionCloudCredentials = new CertificateCloudCredentials(subscriptionId, managementCertificate);
            storageClient = new StorageManagementClient(subscriptionCloudCredentials);
            pathToPackage = TestEnvironment.GetTestPath("Packages", "Octopus.Sample.AzureCloudService.6.0.0.nupkg");

            await storageClient.StorageAccounts.CreateAsync(new StorageAccountCreateParameters(storageName, "test")
            {
                Location = "West US",
                AccountType = "Standard_LRS"
            });
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await storageClient.StorageAccounts.DeleteAsync(storageName);

            storageClient.Dispose();
            managementCertificate.Dispose();
        }

        [Test]
        public async Task Deploy_Package_To_Stage()
        {
            var serviceName = $"{nameof(DeployAzureCloudServiceCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
            var deploymentSlot = DeploymentSlot.Staging;

            using var client = new ComputeManagementClient(subscriptionCloudCredentials);
            try
            {
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test") { Location = "West US" });

                await Deploy();
                await EnsureDeploymentStatus(DeploymentStatus.Running, client, serviceName, deploymentSlot);

                // Suspend state
                var operation = await client.Deployments.UpdateStatusByDeploymentSlotAsync(serviceName, deploymentSlot, new DeploymentUpdateStatusParameters(UpdatedDeploymentStatus.Suspended));
                await WaitForOperation(client, operation);

                //Run again to test upgrading an existing slot and status should not change
                await Deploy();

                await EnsureDeploymentStatus(DeploymentStatus.Suspended, client, serviceName, deploymentSlot);
            }
            finally
            {
                await DeleteDeployment(client, serviceName, deploymentSlot);
                await client.HostedServices.DeleteAsync(serviceName);
            }



            async Task Deploy()
            {
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

                                                         context.WithPackage(pathToPackage, "Octopus.Sample.AzureCloudService", "6.0.0");
                                                     })
                                        .Execute();
            }
        }

        [Test]
        public async Task Deploy_Package_To_Stage_And_Swap()
        {
            var serviceName = $"{nameof(DeployAzureCloudServiceCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";

            using var client = new ComputeManagementClient(subscriptionCloudCredentials);
            try
            {
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test") { Location = "West US" });
                await CommandTestBuilder.CreateAsync<DeployAzureCloudServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.StorageAccountName, storageName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.Slot, DeploymentSlot.Staging.ToString());
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SwapIfPossible, bool.FalseString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.UseCurrentInstanceCount, bool.FalseString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.DeploymentLabel, "v1.0.0");


                                                         context.WithPackage(pathToPackage, "Octopus.Sample.AzureCloudService", "6.0.0");
                                                     })
                                        .Execute();

                await CommandTestBuilder.CreateAsync<DeployAzureCloudServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateThumbprint, managementCertificate.Thumbprint);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CertificateBytes, certificate);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.CloudServiceName, serviceName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.StorageAccountName, storageName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.Slot, DeploymentSlot.Production.ToString());
                                                         context.Variables.Add(SpecialVariables.Action.Azure.SwapIfPossible, bool.TrueString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.UseCurrentInstanceCount, bool.FalseString);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.DeploymentLabel, "v1.0.0");

                                                         context.WithPackage(pathToPackage, "Octopus.Sample.AzureCloudService", "6.0.0");
                                                     })
                                        .Execute();

                await EnsureDeploymentStatus(DeploymentStatus.Running, client, serviceName, DeploymentSlot.Production);

                Func<Task> act = async () => await client.Deployments.GetBySlotAsync(serviceName, DeploymentSlot.Staging);

                (await act.Should().ThrowAsync<CloudException>())
                   .WithMessage("ResourceNotFound: No deployments were found.");
            }
            finally
            {
                await DeleteDeployment(client, serviceName, DeploymentSlot.Production);
                await client.HostedServices.DeleteAsync(serviceName);
            }
        }

         [Test]
        public async Task Deploy_Ensure_Tools_Are_Configured()
        {
            var serviceName = $"{nameof(DeployAzureCloudServiceCommandFixture)}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
            var deploymentSlot = DeploymentSlot.Staging;

            using var client = new ComputeManagementClient(subscriptionCloudCredentials);
            try
            {
                await client.HostedServices.CreateAsync(new HostedServiceCreateParameters(serviceName, "test") { Location = "West US" });

                await Deploy();
            }
            finally
            {
                await DeleteDeployment(client, serviceName, deploymentSlot);
                await client.HostedServices.DeleteAsync(serviceName);
            }

            async Task Deploy()
            {
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

                                                         context.WithPackage(pathToPackage, "Octopus.Sample.AzureCloudService", "6.0.0");

                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                         context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.FSharp), "printfn \"Hello from F#\"");
                                                         context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PreDeploy, ScriptSyntax.CSharp), "Console.WriteLine(\"Hello from C#\");");
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.FullLog.Should().Contain("Hello from C#");
                                                        result.FullLog.Should().Contain("Hello from F#");
                                                    })
                                        .Execute();
            }
        }

        static async Task DeleteDeployment(ComputeManagementClient client, string serviceName, DeploymentSlot deploymentSlot)
        {
            try
            {
                var operation = await client.Deployments.DeleteBySlotAsync(serviceName, deploymentSlot);
                await WaitForOperation(client, operation);
            }
            catch
            {
                // Ignore
            }
        }

        static async Task WaitForOperation(ComputeManagementClient client, OperationStatusResponse operation)
        {
            var maxWait = 30; // 1 minute
            while (maxWait-- >= 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                var operationStatus = await client.GetOperationStatusAsync(operation.RequestId);

                if (operationStatus.Status == OperationStatus.InProgress)
                {
                    continue;
                }

                break;
            }
        }

        async Task EnsureDeploymentStatus(DeploymentStatus requiredStatus, ComputeManagementClient client, string serviceName, DeploymentSlot deploymentSlot)
        {
            DeploymentStatus status;
            var counter = 0;
            do
            {
                var deployment = await client.Deployments.GetBySlotAsync(serviceName, deploymentSlot);
                status = deployment.Status;
                if (status == requiredStatus)
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
            } while (counter++ < 5);
            status.Should().Be(requiredStatus);
        }

        static X509Certificate2 CreateManagementCertificate(string certificate)
        {
            var bytes = Convert.FromBase64String(certificate);
            return new X509Certificate2(bytes);
        }
    }
}