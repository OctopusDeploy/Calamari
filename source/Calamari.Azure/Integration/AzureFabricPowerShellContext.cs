using System;
using System.IO;
using System.Reflection;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Octostache;
using Calamari.Deployment;
using Calamari.Azure.Util;

namespace Calamari.Azure.Integration
{
    public class AzureFabricPowerShellContext
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;

        public AzureFabricPowerShellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.embeddedResources = new AssemblyEmbeddedResources();
        }

        public CommandResult ExecuteScript(IScriptEngine scriptEngine, Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            if (!ServiceFabricHelper.IsServiceFabricSDKKeyInRegistry())
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusAzureTargetScript", "\"" + script.File + "\"");
            variables.Set("OctopusAzureTargetScriptParameters", script.Parameters);

            // Azure PS modules are required for looking up Azure environments (needed for AAD url lookup in Service Fabric world).
            SetAzurePowerShellModulesLoadingMethod(variables);

            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.Azure.FabricConnectionEndpoint), variables);
            SetOutputVariable("OctopusFabricSecurityMode", variables.Get(SpecialVariables.Action.Azure.FabricSecurityMode), variables);
            SetOutputVariable("OctopusFabricServerCertThumbprint", variables.Get(SpecialVariables.Action.Azure.FabricServerCertThumbprint), variables);
            SetOutputVariable("OctopusFabricClientCertThumbprint", variables.Get(SpecialVariables.Action.Azure.FabricClientCertThumbprint), variables);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.Azure.FabricCertificateFindType, "FindByThumbprint"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.Azure.FabricCertificateStoreLocation, "LocalMachine"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.Azure.FabricCertificateStoreName, "MY"), variables);
            SetOutputVariable("OctopusFabricAadClientId", variables.Get(SpecialVariables.Action.Azure.FabricAadClientId), variables);
            SetOutputVariable("OctopusFabricAadResourceUrl", variables.Get(SpecialVariables.Action.Azure.FabricAadResourceUrl), variables);
            SetOutputVariable("OctopusFabricAadTenantId", variables.Get(SpecialVariables.Action.Azure.FabricAadTenantId), variables);

            // Azure AD environment override.
            var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.FabricAadEnvironment, AzurePowerShellContext.DefaultAzureEnvironment);
            if (azureEnvironment != AzurePowerShellContext.DefaultAzureEnvironment)
            {
                Log.Info("Using Azure Environment override - {0}", azureEnvironment);
            }
            SetOutputVariable("OctopusFabricAadEnvironment", azureEnvironment, variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return scriptEngine.Execute(new Script(contextScriptFile.FilePath), variables, commandLineRunner);
            }
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureFabricContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.AzureFabricContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        static void SetAzurePowerShellModulesLoadingMethod(VariableDictionary variables)
        {
            // By default use the Azure PowerShell modules bundled with Calamari
            // If the flag below is set to 'false', then we will rely on PowerShell module auto-loading to find the Azure modules installed on the server
            SetOutputVariable("OctopusUseBundledAzureModules", variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzurePowerShellModules, true).ToString(), variables);
            SetOutputVariable(SpecialVariables.Action.Azure.Output.ModulePath, AzurePowerShellContext.BuiltInAzurePowershellModulePath, variables);
        }

        static void SetOutputVariable(string name, string value, VariableDictionary variables)
        {
            if (variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, variables);
            }
        }
    }
}