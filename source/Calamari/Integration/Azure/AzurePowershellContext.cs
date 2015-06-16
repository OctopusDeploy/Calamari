using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Azure
{
    public class AzurePowershellContext
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICertificateStore certificateStore;
        readonly ICalamariEmbeddedResources embeddedResources;

        const string certificateFileName = "azure_certificate.pfx";
        const int passwordSizeBytes = 20;

        static readonly string azurePowershellModulePath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "AzurePowershell", "ServiceManagement\\Azure\\Azure.psd1"); 

        public AzurePowershellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.certificateStore = new CalamariCertificateStore();
            this.embeddedResources = new ExecutingAssemblyEmbeddedResources();
        }

        public string CreateAzureContextScript(string targetScriptFile, VariableDictionary variables)
        {
            var workingDirectory = Path.GetDirectoryName(targetScriptFile);
            EnsureVariablesSet(workingDirectory, variables);
            variables.Set("OctopusAzureTargetScript", targetScriptFile);

            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureContext.ps1");
            if (!fileSystem.FileExists(azureContextScriptFile))
            {
                fileSystem.OverwriteFile(azureContextScriptFile, embeddedResources.GetEmbeddedResourceText("Calamari.Scripts.AzureContext.ps1"));
            }

            return azureContextScriptFile;
        }

        void EnsureVariablesSet(string workingDirectory, VariableDictionary variables)
        {
            // If the certificate-file exists, we assume all variables have also been set
            if (EnsureCertificateFileExists(workingDirectory, variables))
                return;

            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.ModulePath, azurePowershellModulePath, variables);
            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionId, variables.Get(SpecialVariables.Action.Azure.SubscriptionId), variables);
            // Use the account-name configured in Octopus as the subscription-data-set name
            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionName, variables.Get(SpecialVariables.Account.Name), variables);
        }

        bool EnsureCertificateFileExists(string workingDirectory, VariableDictionary variables)
        {
            var certificateFilePath = Path.Combine(workingDirectory, certificateFileName);

            if (fileSystem.FileExists(certificateFileName))
                return true;

            var azureCertificate = certificateStore.GetOrAdd(
                variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                variables.Get(SpecialVariables.Action.Azure.CertificateBytes));

            var certificatePassword = GenerateCertificatePassword();

            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificatePassword));

            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.CertificateFileName, certificateFilePath, variables);
            Log.SetOutputVariable(SpecialVariables.Action.Azure.Output.CertificatePassword, certificatePassword, variables);

            return false;
        }

        static string GenerateCertificatePassword()
        {
            var random = RandomNumberGenerator.Create();
            var bytes = new byte[passwordSizeBytes]; 
            random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

    }
}