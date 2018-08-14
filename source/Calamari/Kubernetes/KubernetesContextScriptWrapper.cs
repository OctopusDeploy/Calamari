using System;
using System.IO;
using System.Reflection;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;
using Octostache;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly ICalamariEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(ICalamariFileSystem filesystem, ICalamariEmbeddedResources embeddedResources)
        {
            this.fileSystem = filesystem;
            this.embeddedResources = embeddedResources;
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

        public bool Enabled(VariableDictionary variables) => !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl, ""));
        
        public void ExecuteScript(IScriptExecutionContext context, Script script, Action<Script> next)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            context.Variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
            context.Variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);
            context.Variables.Set("Octopus.Action.Kubernetes.KubectlConfig", Path.Combine(workingDirectory, "kubectl-octo.yml"));
            
            using (var contextScriptFile = new TemporaryFile(this.fileSystem, CreateContextScriptFile(workingDirectory, context.ScriptSyntax)))
            {
                next(new Script(contextScriptFile.FilePath));
            }
        }
    }

  
    public class TemporaryFile : IDisposable
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly string filePath;

        public TemporaryFile(ICalamariFileSystem fileSystem, string filePath)
        {
            this.fileSystem = fileSystem;
            this.filePath = filePath;
        }

        public string DirectoryPath => "file://" + Path.GetDirectoryName(FilePath);

        public string FilePath => filePath;

        public void Dispose()
        {
            this.fileSystem.DeleteFile(filePath, FailureOptions.IgnoreFailure);
        }
    }
}
