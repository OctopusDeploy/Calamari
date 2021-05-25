using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly WindowsPhysicalFileSystem fileSystem;
        readonly AssemblyEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(IVariables variables)
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.embeddedResources = new AssemblyEmbeddedResources();
            this.variables = variables;
        }

        public int Priority => ScriptWrapperPriorities.ToolConfigPriority;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public bool IsEnabled(ScriptSyntax syntax)
        {
            return (!string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl, "")) ||
                    !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName, "")) ||
                    !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName, "")));
        }

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
            variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);
            variables.Set("Octopus.Action.Kubernetes.KubectlConfig", Path.Combine(workingDirectory, "kubectl-octo.yml"));

            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory, scriptSyntax)))
            {
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
            }
        }

        string CreateContextScriptFile(string workingDirectory, ScriptSyntax syntax)
        {
            string contextFile;
            switch (syntax)
            {
                case ScriptSyntax.Bash:
                    contextFile = "KubectlBashContext.sh";
                    break;
                case ScriptSyntax.PowerShell:
                    contextFile = "KubectlPowershellContext.ps1";
                    break;
                default:
                    throw new InvalidOperationException($"No kubernetes context wrapper exists for {syntax}");
            }

            var k8sContextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Kubernetes.Scripts.{contextFile}");
            fileSystem.OverwriteFile(k8sContextScriptFile, contextScript);
            return k8sContextScriptFile;
        }
    }
}
