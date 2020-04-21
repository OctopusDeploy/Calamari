using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Hooks;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : ScriptWrapperBase
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

        public override int Priority => ScriptWrapperPriorities.ToolConfigPriority;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public override bool IsEnabled(ScriptSyntax syntax)
        {
            return (!string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl, "")) ||
                    !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName, "")) ||
                    !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName, "")));
        }

        public override IScriptWrapper NextWrapper { get; set; }

        protected override CommandResult ExecuteScriptBase(Script script,
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
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, Variables, environmentVars);
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
                    throw new InvalidOperationException("No kubernetes context wrapper exists for "+ syntax);
            }

            var k8sContextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Kubernetes.Scripts.{contextFile}");
            fileSystem.OverwriteFile(k8sContextScriptFile, contextScript);
            return k8sContextScriptFile;
        }
    }
}
