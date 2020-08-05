﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureScripting
{
    public class AzureContextScriptWrapper : IScriptWrapper
    {
        const string CertificateFileName = "azure_certificate.pfx";
        const int PasswordSizeBytes = 20;
        const string DefaultAzureEnvironment = "AzureCloud";

        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;
        readonly IVariables variables;
        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell, ScriptSyntax.Bash};

        public AzureContextScriptWrapper(IVariables variables, ICalamariFileSystem fileSystem, ICalamariEmbeddedResources embeddedResources)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
        }

        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public bool IsEnabled(ScriptSyntax syntax) => supportedScriptSyntax.Contains(syntax);

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            variables.Set("OctopusAzureTargetScript", script.File);
            variables.Set("OctopusAzureTargetScriptParameters", script.Parameters);

            SetOutputVariable("OctopusAzureSubscriptionId", variables.Get(SpecialVariables.Action.Azure.SubscriptionId), variables);
            SetOutputVariable("OctopusAzureStorageAccountName", variables.Get(SpecialVariables.Action.Azure.StorageAccountName), variables);
            var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment, DefaultAzureEnvironment);
            if (azureEnvironment != DefaultAzureEnvironment)
            {
                Log.Info("Using Azure Environment override - {0}", azureEnvironment);
            }
            SetOutputVariable("OctopusAzureEnvironment", azureEnvironment, variables);

            SetOutputVariable("OctopusAzureExtensionsDirectory",
                variables.Get(SpecialVariables.Action.Azure.ExtensionsDirectory), variables);

            using (new TemporaryFile(Path.Combine(workingDirectory, "AzureProfile.json")))
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory, scriptSyntax)))
            {
                if (variables.Get(SpecialVariables.Account.AccountType) == "AzureServicePrincipal")
                {
                    SetOutputVariable("OctopusUseServicePrincipal", true.ToString(), variables);
                    SetOutputVariable("OctopusAzureADTenantId", variables.Get(SpecialVariables.Action.Azure.TenantId), variables);
                    SetOutputVariable("OctopusAzureADClientId", variables.Get(SpecialVariables.Action.Azure.ClientId), variables);
                    variables.Set("OctopusAzureADPassword", variables.Get(SpecialVariables.Action.Azure.Password));
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
                }

                //otherwise use management certificate
                SetOutputVariable("OctopusUseServicePrincipal", false.ToString(), variables);
                using (new TemporaryFile(CreateAzureCertificate(workingDirectory)))
                {
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
                }
            }
        }

        string CreateContextScriptFile(string workingDirectory, ScriptSyntax syntax)
        {
            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = "AzureContext.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = "AzureContext.ps1";
                    break;
                default:
                    throw new InvalidOperationException($"No Azure context wrapper exists for {syntax}");
            }

            var azureContextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(GetType().Assembly, $"{GetType().Namespace}.Scripts.{contextFile}");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }

        string CreateAzureCertificate(string workingDirectory)
        {
            var certificateFilePath = Path.Combine(workingDirectory, CertificateFileName);
            var certificatePassword = GenerateCertificatePassword();
            var azureCertificate = CalamariCertificateStore.GetOrAdd(variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                                                                     Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes)),
                                                                     StoreName.My);

            variables.Set("OctopusAzureCertificateFileName", certificateFilePath);
            variables.Set("OctopusAzureCertificatePassword", certificatePassword);

            fileSystem.WriteAllBytes(certificateFilePath, azureCertificate.Export(X509ContentType.Pfx, certificatePassword));
            return certificateFilePath;
        }

        static void SetOutputVariable(string name, string value, IVariables variables)
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