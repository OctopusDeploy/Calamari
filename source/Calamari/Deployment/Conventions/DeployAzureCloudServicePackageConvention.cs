using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Integration.Certificates;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class DeployAzureCloudServicePackageConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICertificateStore certificateStore;
        readonly static RandomNumberGenerator RandomNumberSource = RandomNumberGenerator.Create();
        const int PasswordSizeBytes = 20;

        public DeployAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources, 
            IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICertificateStore certificateStore)
        {
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.certificateStore = certificateStore;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.Info("Config file: " + deployment.Variables.Get("OctopusAzureConfigurationFile"));
            string certificateFilePassword;
            lock (RandomNumberSource)
            {
                var bytes = new byte[PasswordSizeBytes];
                RandomNumberSource.GetBytes(bytes);
                certificateFilePassword = Convert.ToBase64String(bytes);
            }

            var certificateFilePath = Path.Combine(deployment.CurrentDirectory, "Certificate.pfx");
            var azureCertificate =
                certificateStore.GetOrAdd(
                    deployment.Variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                    deployment.Variables.Get(SpecialVariables.Action.Azure.CertificateBytes));
            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificateFilePassword));

            var subscriptionName = Guid.NewGuid().ToString();

            var azurePowerShellFolder = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "AzurePowershell"); 

            deployment.Variables.SetOutputVariable("OctopusAzureModulePath", Path.Combine(azurePowerShellFolder, "Azure.psd1"));
            deployment.Variables.SetOutputVariable("OctopusAzureCertificateFileName", certificateFilePath);
            deployment.Variables.SetOutputVariable("OctopusAzureCertificatePassword", certificateFilePassword);
            deployment.Variables.SetOutputVariable("OctopusAzureSubscriptionId", deployment.Variables.Get(SpecialVariables.Action.Azure.SubscriptionId));
            deployment.Variables.SetOutputVariable("OctopusAzureSubscriptionName", subscriptionName);
            deployment.Variables.SetOutputVariable("OctopusAzureServiceName", deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServiceName));
            deployment.Variables.SetOutputVariable("OctopusAzureStorageAccountName", deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName));
            deployment.Variables.SetOutputVariable("OctopusAzureSlot", deployment.Variables.Get(SpecialVariables.Action.Azure.Slot));
            deployment.Variables.SetOutputVariable("OctopusAzurePackageUri", deployment.Variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri));
            deployment.Variables.SetOutputVariable("OctopusAzureDeploymentLabel", deployment.Variables.Get(SpecialVariables.Action.Name) + " v" + deployment.Variables.Get(SpecialVariables.Release.Number));
            deployment.Variables.SetOutputVariable("OctopusAzureSwapIfPossible", deployment.Variables.Get(SpecialVariables.Action.Azure.SwapIfPossible, defaultValue: false.ToString()));
            deployment.Variables.SetOutputVariable("OctopusAzureUseCurrentInstanceCount", deployment.Variables.Get(SpecialVariables.Action.Azure.UseCurrentInstanceCount));

            // The script name 'DeployToAzure.ps1' is used for backwards-compatibility
            var scriptFile = Path.Combine(deployment.CurrentDirectory, "DeployToAzure.ps1");

            // The user may supply the script, to override behaviour
            if (!fileSystem.FileExists(scriptFile))
            {
               fileSystem.OverwriteFile(scriptFile, embeddedResources.GetEmbeddedResourceText("Calamari.Scripts.DeployAzureCloudService.ps1")); 
            }

            // Write the bootstrap script to disk
            var bootstrapScript = Path.Combine(deployment.CurrentDirectory, "BootstrapDeployToAzure.ps1"); 
            fileSystem.OverwriteFile(bootstrapScript,
                embeddedResources.GetEmbeddedResourceText("Calamari.Scripts.BootstrapDeployAzureCloudService.ps1"));

            Log.VerboseFormat("Executing '{0}'", bootstrapScript);
            var result = scriptEngine.Execute(bootstrapScript, deployment.Variables, commandLineRunner);

            fileSystem.DeleteFile(scriptFile, DeletionOptions.TryThreeTimesIgnoreFailure);

            if (result.ExitCode != 0)
            {
                throw new CommandException(string.Format("Script '{0}' returned non-zero exit code: {1}", scriptFile,
                    result.ExitCode));
            }
        }
    }
}