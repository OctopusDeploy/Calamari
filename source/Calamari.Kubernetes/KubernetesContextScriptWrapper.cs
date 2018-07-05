using System;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using Calamari.Hooks;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        private readonly CalamariVariableDictionary variables;
        private readonly WindowsPhysicalFileSystem fileSystem;
        private readonly AssemblyEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(CalamariVariableDictionary variables)
        {
            this.fileSystem = new WindowsPhysicalFileSystem();
            this.embeddedResources = new AssemblyEmbeddedResources();
            this.variables = variables;
        }

        public bool Enabled => !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl, ""));
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
            variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);
            variables.Set("Octopus.Action.Kubernetes.KubectlConfig", Path.Combine(workingDirectory, "kubectl-octo.yml"));
            
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory, scriptSyntax)))
            {
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, variables, commandLineRunner, environmentVars);
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

            var azureContextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Kubernetes.Scripts.{contextFile}");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }
    }
}
