﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Azure.ServiceFabric.Util;
using Calamari.Commands.Support;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Hooks;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Azure.ServiceFabric.Integration
{
    public class AzureServiceFabricPowerShellContext : ScriptWrapperBase
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IVariables variables;

        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell};

        public AzureServiceFabricPowerShellContext(IVariables variables)
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.embeddedResources = new AssemblyEmbeddedResources();
            this.variables = variables;
        }

        public override int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public override bool IsEnabled(ScriptSyntax syntax) =>
            !string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint)) &&
            supportedScriptSyntax.Contains(syntax);

        public override IScriptWrapper NextWrapper { get; set; }

        protected override CommandResult ExecuteScriptBase(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            // We only execute this hook if the connection endpoint has been set
            if (!IsEnabled(scriptSyntax))
            {
                throw new InvalidOperationException(
                    "This script wrapper hook is not enabled, and should not have been run");
            }

            if (!ServiceFabricHelper.IsServiceFabricSdkKeyInRegistry())
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusFabricTargetScript", script.File);
            variables.Set("OctopusFabricTargetScriptParameters", script.Parameters);

            // Azure PS modules are required for looking up Azure environments (needed for AAD url lookup in Service Fabric world).
            SetAzureModulesLoadingMethod(variables);

            // Read thumbprint from our client cert variable (if applicable).
            var securityMode = variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode);
            var clientCertThumbprint = string.Empty;
            if (securityMode == AzureServiceFabricSecurityMode.SecureClientCertificate.ToString())
            {
                var certificateVariable = GetMandatoryVariable(variables, SpecialVariables.Action.ServiceFabric.ClientCertVariable);
                clientCertThumbprint = variables.Get($"{certificateVariable}.{CertificateVariables.Properties.Thumbprint}");
            }

            // Set output variables for our script to access.
            SetOutputVariable("OctopusFabricConnectionEndpoint", variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint), variables);
            SetOutputVariable("OctopusFabricSecurityMode", variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode), variables);
            SetOutputVariable("OctopusFabricServerCertThumbprint", variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint), variables);
            SetOutputVariable("OctopusFabricClientCertThumbprint", clientCertThumbprint, variables);
            SetOutputVariable("OctopusFabricCertificateFindType", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindType, "FindByThumbprint"), variables);
            SetOutputVariable("OctopusFabricCertificateFindValueOverride", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateFindValueOverride), variables);
            SetOutputVariable("OctopusFabricCertificateStoreLocation", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, "LocalMachine"), variables);
            SetOutputVariable("OctopusFabricCertificateStoreName", variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName, "MY"), variables);
            SetOutputVariable("OctopusFabricAadCredentialType", variables.Get(SpecialVariables.Action.ServiceFabric.AadCredentialType), variables);
            SetOutputVariable("OctopusFabricAadClientCredentialSecret", variables.Get(SpecialVariables.Action.ServiceFabric.AadClientCredentialSecret), variables);
            SetOutputVariable("OctopusFabricAadUserCredentialUsername", variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername), variables);
            SetOutputVariable("OctopusFabricAadUserCredentialPassword", variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword), variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, Variables, environmentVars);
            }
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.AzureServiceFabricContext.ps1");
            var contextScript = embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Assembly.GetName().Name}.Scripts.AzureServiceFabricContext.ps1");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        static void SetAzureModulesLoadingMethod(IVariables variables)
        {
            // We don't bundle the standard Azure PS module for Service Fabric work. We do however need
            // a certain Active Directory library that is bundled with Calamari.
            SetOutputVariable("OctopusFabricActiveDirectoryLibraryPath", Path.GetDirectoryName(typeof(AzureServiceFabricPowerShellContext).Assembly.Location), variables);
        }

        static void SetOutputVariable(string name, string value, IVariables variables)
        {
            if (variables.Get(name) != value)
            {
                Log.SetOutputVariable(name, value, variables);
            }
        }

        string GetMandatoryVariable(IVariables variables, string variableName)
        {
            var value = variables.Get(variableName);

            if (string.IsNullOrWhiteSpace(value))
                throw new CommandException($"Variable {variableName} was not supplied");

            return value;
        }
    }
}