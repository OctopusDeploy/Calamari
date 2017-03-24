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
using Calamari.Commands.Support;

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

            var securityMode = variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode);
            if (securityMode == "SecureClientCertificate")
            {
                var certificateVariable = GetMandatoryVariable(variables, SpecialVariables.Action.Certificate.CertificateVariable);
                var thumbprint = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Thumbprint}");
                SetOutputVariable("OctopusFabricClientCertVariable", variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertVariable), variables);
            }


            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint), variables);
            SetOutputVariable("OctopusFabricSecurityMode", variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode), variables);
            SetOutputVariable("OctopusFabricServerCertThumbprint", variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint), variables);
            SetOutputVariable("OctopusFabricClientCertVariable", variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertVariable), variables);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindType, "FindByThumbprint"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, "LocalMachine"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName, "MY"), variables);
            SetOutputVariable("OctopusFabricAadCredentialType", variables.Get(SpecialVariables.Action.ServiceFabric.AadCredentialType), variables);
            SetOutputVariable("OctopusFabricAadClientCredentialSecret", variables.Get(SpecialVariables.Action.ServiceFabric.AadClientCredentialSecret), variables);
            SetOutputVariable("OctopusFabricAadUserCredentialUsername", variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername), variables);
            SetOutputVariable("OctopusFabricAadUserCredentialPassword", variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword), variables);

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
            // We don't bundle the standard Azure PS module for Service Fabric work. We do however need
            // a certain Active Directory library that is bundled with Calamari.
            SetOutputVariable("OctopusFabricActiveDirectoryLibraryPath", Path.GetDirectoryName(typeof(Program).Assembly.Location), variables);
        }

        static void SetOutputVariable(string name, string value, VariableDictionary variables)
        {
            if (variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, variables);
            }
        }

        string GetMandatoryVariable(CalamariVariableDictionary variables, string variableName)
        {
            var value = variables.Get(variableName);

            if (string.IsNullOrWhiteSpace(value))
                throw new CommandException($"Variable {variableName} was not supplied");

            return value;
        }
    }
}