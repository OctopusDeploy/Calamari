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

            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.Azure.FabricConnectionEndpoint), variables);
            SetOutputVariable("OctopusFabricIsSecure", variables.Get(SpecialVariables.Action.Azure.FabricIsSecure), variables);
            SetOutputVariable("OctopusFabricServerCertThumbprint", variables.Get(SpecialVariables.Action.Azure.FabricServerCertThumbprint), variables);
            SetOutputVariable("OctopusFabricClientCertThumbprint", variables.Get(SpecialVariables.Action.Azure.FabricClientCertThumbprint), variables);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.Azure.FabricCertificateFindType, "FindByThumbprint"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.Azure.FabricCertificateStoreLocation, "LocalMachine"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.Azure.FabricCertificateStoreName, "MY"), variables);

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

        static void SetOutputVariable(string name, string value, VariableDictionary variables)
        {
            if (variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, variables);
            }
        }
    }
}