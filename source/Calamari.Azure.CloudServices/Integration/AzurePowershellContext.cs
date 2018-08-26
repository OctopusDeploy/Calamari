using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Hooks;
using Calamari.Integration.Certificates;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octostache;

namespace Calamari.Azure.CloudServices.Integration
{
    public class AzurePowerShellContext : IScriptWrapper
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICertificateStore certificateStore;
        readonly ICalamariEmbeddedResources embeddedResources;
        private readonly CalamariVariableDictionary variables;

        const string CertificateFileName = "azure_certificate.pfx";
        const int PasswordSizeBytes = 20;

        public const string DefaultAzureEnvironment = "AzureCloud";

        public AzurePowerShellContext(CalamariVariableDictionary variables)
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.certificateStore = new CalamariCertificateStore();
            this.embeddedResources = new AssemblyEmbeddedResources();
            this.variables = variables;
        }

        public bool Enabled => variables.Get(SpecialVariables.Account.AccountType, "").StartsWith("Azure") &&
                               string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint));

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            // Only run this hook if we have an azure account
            if (!Enabled)
            {
                throw new InvalidOperationException(
                    "This script wrapper hook is not enabled, and should not have been run");
            }

            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusAzureTargetScript", "\"" + script.File + "\"");
            variables.Set("OctopusAzureTargetScriptParameters", script.Parameters);

            SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionId, variables.Get(SpecialVariables.Action.Azure.SubscriptionId), variables);
            SetOutputVariable("OctopusAzureStorageAccountName", variables.Get(SpecialVariables.Action.Azure.StorageAccountName), variables);
            var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment, DefaultAzureEnvironment);
            if (azureEnvironment != DefaultAzureEnvironment)
            {
                Log.Info("Using Azure Environment override - {0}", azureEnvironment);
            }
            SetOutputVariable("OctopusAzureEnvironment", azureEnvironment, variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                if (variables.Get(SpecialVariables.Account.AccountType) == "AzureServicePrincipal")
                {
                    SetOutputVariable("OctopusUseServicePrincipal", true.ToString(), variables);
                    SetOutputVariable("OctopusAzureADTenantId", variables.Get(SpecialVariables.Action.Azure.TenantId), variables);
                    SetOutputVariable("OctopusAzureADClientId", variables.Get(SpecialVariables.Action.Azure.ClientId), variables);
                    variables.Set("OctopusAzureADPassword", variables.Get(SpecialVariables.Action.Azure.Password));
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, variables, commandLineRunner, environmentVars);
                }

                //otherwise use management certificate
                SetOutputVariable("OctopusUseServicePrincipal", false.ToString(), variables);
                using (new TemporaryFile(CreateAzureCertificate(workingDirectory, variables)))
                {
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, variables, commandLineRunner, environmentVars);
                }
            }
        }
      
        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.AzureContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        private string CreateAzureCertificate(string workingDirectory, VariableDictionary variables)
        {
            var certificateFilePath = Path.Combine(workingDirectory, CertificateFileName);
            var certificatePassword = GenerateCertificatePassword();
            var azureCertificate = certificateStore.GetOrAdd(
                variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes)),
                StoreName.My);

            variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
            variables.Set("OctopusAzureCertificatePassword", certificatePassword);

            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificatePassword));
            return certificateFilePath;
        }

        static void SetOutputVariable(string name, string value, VariableDictionary variables)
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