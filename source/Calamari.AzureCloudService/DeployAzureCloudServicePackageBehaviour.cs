using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.WindowsAzure.Management.Compute;
using Hyak.Common;
using Microsoft.WindowsAzure.Management.Compute.Models;

namespace Calamari.AzureCloudService
{
    public class DeployAzureCloudServicePackageBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureCloudServicePackageBehaviour(ILog log,
                                                       ICalamariFileSystem fileSystem,
                                                       IScriptEngine scriptEngine,
                                                       ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            static async Task<bool> Exists(IComputeManagementClient azureClient, string serviceName, DeploymentSlot deploymentSlot)
            {
                try
                {
                    var deployment = await azureClient.Deployments.GetBySlotAsync(serviceName, deploymentSlot);
                    if (deployment == null)
                    {
                        return false;
                    }
                }
                catch (CloudException e)
                {
                    if (e.Error.Code == "ResourceNotFound")
                    {
                        return false;
                    }

                    throw;
                }

                return true;
            }

            var configurationFile = context.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile);
            var cloudServiceName = context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var slot = context.Variables.Get(SpecialVariables.Action.Azure.Slot, DeploymentSlot.Staging.ToString());
            var packageUri = context.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri);
            var deploymentLabel = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentLabel, $"{context.Variables.Get(ActionVariables.Name)} v{context.Variables.Get(KnownVariables.Release.Number)}");

            log.Info($"Config file: {configurationFile}");

            log.SetOutputVariable("OctopusAzureServiceName", cloudServiceName, context.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), context.Variables);
            log.SetOutputVariable("OctopusAzureSlot", slot, context.Variables);
            log.SetOutputVariable("OctopusAzurePackageUri", packageUri, context.Variables);
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deploymentLabel, context.Variables);
            log.SetOutputVariable("OctopusAzureSwapIfPossible", context.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, bool.FalseString), context.Variables);
            log.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", context.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount), context.Variables);

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(context.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (fileSystem.FileExists(scriptFile))
            {
                var result = scriptEngine.Execute(new Script(scriptFile), context.Variables, commandLineRunner);

                fileSystem.DeleteFile(scriptFile, FailureOptions.IgnoreFailure);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"Script '{scriptFile}' returned non-zero exit code: {result.ExitCode}");
                }

                return;
            }

            var account = new AzureAccount(context.Variables);
            var certificate = CalamariCertificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes);
            var deploymentSlot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), slot);

            using var azureClient = account.CreateComputeManagementClient(certificate);
            var exists = await Exists(azureClient, cloudServiceName, deploymentSlot);

            if (!exists)
            {
                log.Verbose("Creating a new deployment...");
                await azureClient.Deployments.CreateAsync(cloudServiceName,
                                                          deploymentSlot,
                                                          new DeploymentCreateParameters
                                                          {
                                                              Label = deploymentLabel,
                                                              Configuration = File.ReadAllText(configurationFile),
                                                              PackageUri = new Uri(packageUri),
                                                              Name = Guid.NewGuid().ToString("N"),
                                                              StartDeployment = true
                                                          });
            }
            else
            {
                log.Verbose($"A deployment already exists in {cloudServiceName} for slot {slot}. Upgrading deployment...");
                await azureClient.Deployments.UpgradeBySlotAsync(cloudServiceName,
                                                                 deploymentSlot,
                                                                 new DeploymentUpgradeParameters
                                                                 {
                                                                     Label = deploymentLabel,
                                                                     Configuration = File.ReadAllText(configurationFile),
                                                                     PackageUri = new Uri(packageUri),
                                                                     Mode = DeploymentUpgradeMode.Auto,
                                                                     Force = true
                                                                 });
            }

            var deployment = await azureClient.Deployments.GetBySlotAsync(cloudServiceName, deploymentSlot);

            log.SetOutputVariable("OctopusAzureCloudServiceDeploymentID", deployment.PrivateId, context.Variables);
            log.SetOutputVariable("OctopusAzureCloudServiceDeploymentUrl", deployment.Uri.ToString(), context.Variables);

            log.Info($"Deployment complete; Deployment ID: {deployment.PrivateId}");
        }
    }
}