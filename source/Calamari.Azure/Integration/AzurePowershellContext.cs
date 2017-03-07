using System;
using System.IO;
using System.Reflection;
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
        const string DefaultAzureEnvironment = "AzureCloud";

        static readonly string BuiltInAzurePowershellModulePath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "PowerShell");

        public AzurePowerShellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.certificateStore = new CalamariCertificateStore();
            this.embeddedResources = new AssemblyEmbeddedResources();
        }

        public CommandResult ExecuteScript(IScriptEngine scriptEngine, Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusAzureTargetScript", "\"" + script.File + "\"");
            variables.Set("OctopusAzureTargetScriptParameters", script.Parameters);

            SetAzureModuleLoadingMethod(variables);

            SetOutputVariable(SpecialVariables.Action.Azure.Output.SubscriptionId, variables.Get(SpecialVariables.Action.Azure.SubscriptionId), variables);
            SetOutputVariable("OctopusAzureStorageAccountName", variables.Get(SpecialVariables.Action.Azure.StorageAccountName), variables);
            SetOutputVariable("OctopusAzureEnvironment",variables.Get(SpecialVariables.Action.Azure.Environment, DefaultAzureEnvironment),variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                if (variables.Get(SpecialVariables.Account.AccountType) == "AzureServicePrincipal")
                {
                    SetOutputVariable("OctopusUseServicePrincipal", true.ToString(), variables);
                    SetOutputVariable("OctopusAzureADTenantId", variables.Get(SpecialVariables.Action.Azure.TenantId), variables);
                    SetOutputVariable("OctopusAzureADClientId", variables.Get(SpecialVariables.Action.Azure.ClientId), variables);
                    variables.Set("OctopusAzureADPassword", variables.Get(SpecialVariables.Action.Azure.Password));
                    return scriptEngine.Execute(new Script(contextScriptFile.FilePath), variables, commandLineRunner);
                }

                //otherwise use management certificate
                SetOutputVariable("OctopusUseServicePrincipal", false.ToString(), variables);
                using (new TemporaryFile(CreateAzureCertificate(workingDirectory, variables)))
                {
                    return scriptEngine.Execute(new Script(contextScriptFile.FilePath), variables, commandLineRunner);
                }
            }
        }

        static void SetAzureModuleLoadingMethod(VariableDictionary variables)
        {
            // By default use the Azure modules bundled with Calamari
            // If the flag below is set to 'false', then we will rely on PowerShell module auto-loading to find the Azure modules installed on the server
            SetOutputVariable("OctopusUseBundledAzureModules", variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzurePowerShellModules, true).ToString(), variables);
            SetOutputVariable(SpecialVariables.Action.Azure.Output.ModulePath, BuiltInAzurePowershellModulePath, variables);
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
                variables.Get(SpecialVariables.Action.Azure.CertificateBytes),
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