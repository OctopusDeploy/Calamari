using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Common.Aws;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly ILog log;
        private readonly Lazy<AwsEnvironmentVariablesFactory> awsEnvironmentVariablesFactory;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(IVariables variables, ILog log,
            Lazy<AwsEnvironmentVariablesFactory> awsEnvironmentVariablesFactory,
            ICalamariEmbeddedResources embeddedResources, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.log = log;
            this.awsEnvironmentVariablesFactory = awsEnvironmentVariablesFactory;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesContextPriority;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public bool IsEnabled(ScriptSyntax syntax)
        {
            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            return hasClusterUrl || hasClusterName;
        }

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
                                           ScriptSyntax scriptSyntax,
                                           ICommandLineRunner commandLineRunner,
                                           Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            if (environmentVars == null)
            {
                environmentVars = new Dictionary<string, string>();
            }

            var setupKubectlAuthentication = new SetupKubectlAuthentication(variables,
                                                                            log,
                                                                            awsEnvironmentVariablesFactory,
                                                                            scriptSyntax,
                                                                            commandLineRunner,
                                                                            environmentVars,
                                                                            workingDirectory);
            var accountType = variables.Get("Octopus.Account.AccountType");

            try
            {
                var result = setupKubectlAuthentication.Execute(accountType);

                if (result.ExitCode != 0)
                {
                    return result;
                }
            }
            catch (CommandLineException)
            {
                return new CommandResult(String.Empty, 1);
            }

            if (scriptSyntax == ScriptSyntax.PowerShell && accountType == "AzureServicePrincipal")
            {
                variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
                variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);

                using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
                {
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
                }
            }

            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            const string contextFile = "AzurePowershellContext.ps1";
            var contextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Kubernetes.Scripts.{contextFile}");
            fileSystem.OverwriteFile(contextScriptFile, contextScript);
            return contextScriptFile;
        }
    }
}
