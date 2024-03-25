#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(IVariables variables, ILog log, ICalamariEmbeddedResources embeddedResources, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
        }

        public int Priority => ScriptWrapperPriorities.KubernetesContextPriority;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public bool IsEnabled(ScriptSyntax syntax)
        {
            var isKubernetesTentacleTarget = string.Equals(variables.Get(MachineVariables.DeploymentTargetType), "KubernetesTentacle", StringComparison.OrdinalIgnoreCase);
            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            return isKubernetesTentacleTarget || hasClusterUrl || hasClusterName;
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

            var kubectl = new Kubectl(variables, log, commandLineRunner, workingDirectory, environmentVars);
            var setupKubectlAuthentication = new SetupKubectlAuthentication(variables,
                                                                            log,
                                                                            commandLineRunner,
                                                                            kubectl,
                                                                            fileSystem,
                                                                            environmentVars,
                                                                            workingDirectory);

            try
            {
                var result = setupKubectlAuthentication.Execute();

                if (result.ExitCode != 0)
                {
                    return result;
                }
            }
            catch (CommandLineException)
            {
                return new CommandResult(String.Empty, 1);
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
#endif
