using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.ResourceStatus;

public class ResourceStatusReportWrapper : IScriptWrapper
{
    readonly IVariables variables;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    private readonly IResourceRetriever retriever;
    private readonly IResourceStatusChecker statusChecker;

    public ResourceStatusReportWrapper(IVariables variables, ILog log, ICalamariFileSystem fileSystem, IResourceRetriever retriever, IResourceStatusChecker statusChecker)
    {
        this.variables = variables;
        this.log = log;
        this.fileSystem = fileSystem;
        this.retriever = retriever;
        this.statusChecker = statusChecker;
    }
    
    public int Priority => ScriptWrapperPriorities.KubernetesStatusCheckPriority;
    public IScriptWrapper NextWrapper { get; set; }
    public bool IsEnabled(ScriptSyntax syntax)
    {
        // variables.Get("");
        var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
        var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
        return hasClusterUrl || hasClusterName;
    }

    public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner,
        Dictionary<string, string> environmentVars)
    {
        var result = NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);

        if (!TryReadManifestFile(out var content))
        {
            return result;
        }

        var definedResources = KubernetesYaml.GetDefinedResources(content).ToList();
        
        statusChecker.CheckStatusUntilCompletion(definedResources, commandLineRunner, log);

        return result;
    }
    
    private bool TryReadManifestFile(out string content)
    {
        var customResourceFileName = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName");
        var knownFileNames = new[]
        {
            "secret.yml", customResourceFileName, "deployment.yml", "service.yml", "ingress.yml",
        };
        foreach (var file in knownFileNames)
        {
            if (!fileSystem.FileExists(file))
            {
                continue;
            }
            content = fileSystem.ReadFile(file);
            return true;
        }
        content = null;
        return false;
    }
}