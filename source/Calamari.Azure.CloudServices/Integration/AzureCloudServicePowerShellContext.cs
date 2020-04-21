using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Hooks;
using Calamari.Integration.Certificates;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Azure.CloudServices.Integration
{
    public class AzureCloudServicePowerShellContext : ScriptWrapperBase
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICertificateStore certificateStore;
        readonly ICalamariEmbeddedResources embeddedResources;

        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell};

        const string CertificateFileName = "azure_certificate.pfx";
        const int PasswordSizeBytes = 20;

        public const string DefaultAzureEnvironment = "AzureCloud";

        public AzureCloudServicePowerShellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.certificateStore = new CalamariCertificateStore();
            this.embeddedResources = new AssemblyEmbeddedResources();
        }

        public override int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public override bool IsEnabled(ScriptSyntax syntax) => Variables.Get(SpecialVariables.Account.AccountType, "").StartsWith("Azure") &&
                                                               string.IsNullOrEmpty(Variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint)) &&
                                                               supportedScriptSyntax.Contains(syntax);

        public override IScriptWrapper NextWrapper { get; set; }

        protected override CommandResult ExecuteScriptBase(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            // Only run this hook if we have an azure account
            if (!IsEnabled(scriptSyntax))
            {
                throw new InvalidOperationException(
                    "This script wrapper hook is not enabled, and should not have been run");
            }

            var workingDirectory = Path.GetDirectoryName(script.File);
            Variables.Set("OctopusAzureTargetScript", script.File);
            Variables.Set("OctopusAzureTargetScriptParameters", script.Parameters);

            SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionId, Variables.Get(SpecialVariables.Action.Azure.SubscriptionId));
            SetOutputVariable("OctopusAzureStorageAccountName", Variables.Get(SpecialVariables.Action.Azure.StorageAccountName));
            var azureEnvironment = Variables.Get(SpecialVariables.Action.Azure.Environment, DefaultAzureEnvironment);
            if (azureEnvironment != DefaultAzureEnvironment)
            {
                Log.Info("Using Azure Environment override - {0}", azureEnvironment);
            }
            SetOutputVariable("OctopusAzureEnvironment", azureEnvironment);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                using (new TemporaryFile(CreateAzureCertificate(workingDirectory)))
                {
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, Variables, environmentVars);
                }
            }
        }
      
        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureCloudServiceContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.AzureCloudServiceContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        private string CreateAzureCertificate(string workingDirectory)
        {
            var certificateFilePath = Path.Combine(workingDirectory, CertificateFileName);
            var certificatePassword = GenerateCertificatePassword();
            var azureCertificate = certificateStore.GetOrAdd(
                Variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                Convert.FromBase64String(Variables.Get(SpecialVariables.Action.Azure.CertificateBytes)),
                StoreName.My);

            Variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
            Variables.Set("OctopusAzureCertificatePassword", certificatePassword);

            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificatePassword));
            return certificateFilePath;
        }

        void SetOutputVariable(string name, string value)
        {
            if (Variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, Variables);
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