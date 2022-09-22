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
using Microsoft.WindowsAzure.Management.Compute.Models;
using Microsoft.WindowsAzure.Management.Compute;
using Hyak.Common;

namespace Calamari.AzureCloudService
{
    public class SwapAzureDeploymentBehaviour : IBeforePackageExtractionBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public SwapAzureDeploymentBehaviour(ILog log, ICalamariFileSystem fileSystem,
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
            static async Task<DeploymentGetResponse> GetStagingDeployment(IComputeManagementClient azureClient, string serviceName)
            {
                try
                {
                    var deployment = await azureClient.Deployments.GetBySlotAsync(serviceName, DeploymentSlot.Staging);
                    return deployment;
                }
                catch (CloudException e)
                {
                    if (e.Error.Code == "ResourceNotFound")
                    {
                        return null;
                    }

                    throw;
                }
            }

            var cloudServiceName = context.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName);
            var slot = context.Variables.Get(SpecialVariables.Action.Azure.Slot, DeploymentSlot.Staging.ToString());
            var deploymentSlot = (DeploymentSlot)Enum.Parse(typeof(DeploymentSlot), slot);
            var swapIfPossible = context.Variables.GetFlag(SpecialVariables.Action.Azure.SwapIfPossible);
            var deploymentLabel = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentLabel, $"{context.Variables.Get(ActionVariables.Name)} v{context.Variables.Get(KnownVariables.Release.Number)}");

            log.SetOutputVariable("OctopusAzureServiceName", cloudServiceName, context.Variables);
            log.SetOutputVariable("OctopusAzureStorageAccountName", context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName), context.Variables);
            log.SetOutputVariable("OctopusAzureSlot", slot, context.Variables);
            log.SetOutputVariable("OctopusAzureDeploymentLabel", deploymentLabel, context.Variables);
            log.SetOutputVariable("OctopusAzureSwapIfPossible", swapIfPossible.ToString(), context.Variables);

            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            var scriptFile = Path.Combine(tempDirectory, "SwapAzureCloudServiceDeployment.ps1");

            // The user may supply the script, to override behaviour
            if (fileSystem.FileExists(scriptFile))
            {
                var result = scriptEngine.Execute(new Script(scriptFile), context.Variables, commandLineRunner);

                fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);

                if (result.ExitCode != 0)
                {
                    throw new CommandException($"Script '{scriptFile}' returned non-zero exit code: {result.ExitCode}");
                }
            }
            else if (swapIfPossible && deploymentSlot == DeploymentSlot.Production)
            {
                log.Verbose("Checking whether a swap is possible");
                var account = new AzureAccount(context.Variables);
                var certificate = CalamariCertificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes);

                using var azureClient = account.CreateComputeManagementClient(certificate);
                var deployment = await GetStagingDeployment(azureClient, cloudServiceName);
                if (deployment == null)
                {
                    log.Verbose("Nothing is deployed in staging");
                }
                else
                {
                    log.Verbose($"Current staging deployment: {deployment.Label}");
                    if (deployment.Label == deploymentLabel)
                    {
                        log.Verbose("Swapping the staging environment to production");
                        await azureClient.Deployments.SwapAsync(cloudServiceName, new DeploymentSwapParameters { SourceDeployment = deployment.PrivateId });
                        log.SetOutputVariable(SpecialVariables.Action.Azure.Output.CloudServiceDeploymentSwapped, bool.TrueString, context.Variables);
                    }
                }
            }

            var swapped = context.Variables.GetFlag(SpecialVariables.Action.Azure.Output.CloudServiceDeploymentSwapped);
            if (swapped)
            {
                context.Variables.Set(KnownVariables.Action.SkipRemainingConventions, bool.TrueString);
            }
        }
    }
}