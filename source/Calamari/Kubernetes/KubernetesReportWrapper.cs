using System.Collections.Generic;
using System.IO;
using System.Threading;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace Calamari.Kubernetes;

public class KubernetesReportWrapper : IScriptWrapper
{
    readonly IVariables variables;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    private readonly IKubernetesResourceStatusChecker statusChecker;

    public KubernetesReportWrapper(IVariables variables, ILog log, ICalamariFileSystem fileSystem, IKubernetesResourceStatusChecker statusChecker)
    {
        this.variables = variables;
        this.log = log;
        this.fileSystem = fileSystem;
        this.statusChecker = statusChecker;
    }
    
    public int Priority => ScriptWrapperPriorities.KubernetesStatusCheckPriority;
    public IScriptWrapper NextWrapper { get; set; }
    public bool IsEnabled(ScriptSyntax syntax)
    {
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
        
        var resource = GetDefinedResource(content);
        
        log.Info($"Deployed: {resource.Kind}/{resource.Name}");
        log.Info("");
        log.Info($"Status checks:");
        var n = 5;
        while (--n >= 0)
        {
            log.Info($"Check #{5 - n}:");
            var statuses = statusChecker.GetHierarchyStatuses(resource, commandLineRunner);
            log.Verbose(new JArray(statuses).ToString());
            DisplayStatuses(statuses);
            Thread.Sleep(2000);
        }

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
            if (fileSystem.FileExists(file))
            {
                content = fileSystem.ReadFile(file);
                return true;
            }
        }
        content = null;
        return false;
    }
    
    private KubernetesResource GetDefinedResource(string manifests)
    {
        var deserializer = new Deserializer();
        var yamlObject = deserializer.Deserialize(new StringReader(manifests));
        
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        var writer = new StringWriter();
        serializer.Serialize(writer, yamlObject);

        var json = writer.ToString();
        var resource = JObject.Parse(json);
        var name = resource.SelectToken("$.metadata.name").Value<string>();
        var kind = resource.SelectToken("$.kind").Value<string>();
        var @namespace = resource.SelectToken("$.metadata.namespace")?.Value<string>()
                         ?? variables.Get(SpecialVariables.Namespace)
                         ?? "default";
        return new KubernetesResource
        {
            Name = name,
            Kind = kind,
            Namespace = @namespace
        };
    }

    private void DisplayStatuses(IEnumerable<JObject> statuses)
    {
        foreach (var status in statuses)
        {
            var kind = status.SelectToken("$.kind").Value<string>();
            var name = status.SelectToken("$.metadata.name").Value<string>();
            log.Info($"Status of {kind}/{name}");

            var statusData = status.SelectToken("$.status").ToString();
            
            log.Info(statusData);
        }
    }
}