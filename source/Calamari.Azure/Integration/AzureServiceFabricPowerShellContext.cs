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
    public class AzureServiceFabricPowerShellContext
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;

        public AzureServiceFabricPowerShellContext()
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.embeddedResources = new AssemblyEmbeddedResources();
        }

        public CommandResult ExecuteScript(IScriptEngine scriptEngine, Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            if (!ServiceFabricHelper.IsServiceFabricSDKKeyInRegistry())
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusFabricTargetScript", "\"" + script.File + "\"");
            variables.Set("OctopusFabricTargetScriptParameters", script.Parameters);

            // Azure PS modules are required for looking up Azure environments (needed for AAD url lookup in Service Fabric world).
            SetAzureModulesLoadingMethod(variables);

            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint), variables);
            SetOutputVariable("OctopusFabricSecurityMode", variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode), variables);
            SetOutputVariable("OctopusFabricServerCertThumbprint", variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint), variables);
            SetOutputVariable("OctopusFabricClientCertThumbprint", variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertThumbprint), variables);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindType, "FindByThumbprint"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, "LocalMachine"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName, "MY"), variables);
            SetOutputVariable("OctopusFabricAadClientId", variables.Get(SpecialVariables.Action.ServiceFabric.AadClientId), variables);
            SetOutputVariable("OctopusFabricAadClientSecret", variables.Get(SpecialVariables.Action.ServiceFabric.AadClientSecret), variables);
            SetOutputVariable("OctopusFabricAadResourceId", variables.Get(SpecialVariables.Action.ServiceFabric.AadResourceId), variables);
            SetOutputVariable("OctopusFabricAadTenantId", variables.Get(SpecialVariables.Action.ServiceFabric.AadTenantId), variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return scriptEngine.Execute(new Script(contextScriptFile.FilePath), variables, commandLineRunner);
            }
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureServiceFabricContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Azure.Scripts.AzureServiceFabricContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        static void SetAzureModulesLoadingMethod(VariableDictionary variables)
        {
            //// By default use the Azure PowerShell modules bundled with Calamari
            //// If the flag below is set to 'false', then we will rely on PowerShell module auto-loading to find the Azure modules installed on the server
            //SetOutputVariable("OctopusUseBundledAzureModules", variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzurePowerShellModules, true).ToString(), variables);
            //SetOutputVariable(SpecialVariables.Action.Azure.Output.ModulePath, AzurePowerShellContext.BuiltInAzurePowershellModulePath, variables);

            // Calamari dll references
            SetOutputVariable("OctopusFabricActiveDirectoryLibraryPath", Path.GetDirectoryName(typeof(Program).Assembly.Location), variables);
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