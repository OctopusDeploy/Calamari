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
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
            variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);
            variables.Set("Octopus.Action.Kubernetes.KubectlConfig", Path.Combine(workingDirectory, "kubectl-octo.yml"));
            
            using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
            {
                return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), variables, commandLineRunner, environmentVars);
            }
        }

        string CreateContextScriptFile(string workingDirectory)
        {

            var azureContextScriptFile = Path.Combine(workingDirectory, "Octopus.KubectlBashContext.sh");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), "Calamari.Kubernetes.Scripts.KubectlBashContext.sh");
            fileSystem.OverwriteFile(azureContextScriptFile, contextScript);
            return azureContextScriptFile;
        }
    }
}
