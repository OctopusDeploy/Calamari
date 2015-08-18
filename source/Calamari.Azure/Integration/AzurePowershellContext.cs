using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octostache;

namespace Calamari.Azure.Integration
{
    public class AzurePowerShellContext
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICertificateStore certificateStore;
        readonly ICalamariEmbeddedResources embeddedResources;

        const string CertificateFileName = "azure_certificate.pfx";
        const int PasswordSizeBytes = 20;

        static readonly string AzurePowershellModulePath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "AzurePowershell", "ServiceManagement\\Azure\\Azure.psd1");

        public AzurePowerShellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.certificateStore = new CalamariCertificateStore();
            this.embeddedResources = new CallingAssemblyEmbeddedResources();
        }

        public CommandResult ExecuteScript(IScriptEngine scriptEngine, string scriptFile, VariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(scriptFile);
            variables.Set("OctopusAzureTargetScript", scriptFile);

            SetOutputVariable(SpecialVariables.Action.Azure.Output.ModulePath, AzurePowershellModulePath, variables);
            SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionId, variables.Get(SpecialVariables.Action.Azure.SubscriptionId), variables);
            SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionName, variables.Get(SpecialVariables.Account.Name), variables);

            using (new TemporaryFile(CreateAzureCertificate(workingDirectory, variables)))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return scriptEngine.Execute(contextScriptFile.FilePath, variables, commandLineRunner);
            }
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText("Calamari.Azure.Scripts.AzureContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        private string CreateAzureCertificate(string workingDirectory, VariableDictionary variables)
        {
            var certificateFilePath = Path.Combine(workingDirectory, CertificateFileName);
            var certificatePassword = GenerateCertificatePassword();
            var azureCertificate = certificateStore.GetOrAdd(
                variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                variables.Get(SpecialVariables.Action.Azure.CertificateBytes),
                StoreName.My);

            variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
            variables.Set("OctopusAzureCertificatePassword", certificatePassword);

            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificatePassword));
            return certificateFilePath;

        }

        void SetOutputVariable(string name, string value, VariableDictionary variables)
        {
            if (variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, variables);
            }
        }

        static string GenerateCertificatePassword()
        {
            var random = RandomNumberGenerator.Create();
            var bytes = new byte[PasswordSizeBytes]; 
            random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}