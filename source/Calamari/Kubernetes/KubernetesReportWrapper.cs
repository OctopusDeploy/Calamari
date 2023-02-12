using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace Calamari.Kubernetes;

public class KubernetesReportWrapper : IScriptWrapper
{
    readonly IVariables variables;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly ICalamariEmbeddedResources embeddedResources;

    public KubernetesReportWrapper(IVariables variables, ILog log, ICalamariEmbeddedResources embeddedResources, ICalamariFileSystem fileSystem)
    {
        this.variables = variables;
        this.log = log;
        this.fileSystem = fileSystem;
        this.embeddedResources = embeddedResources;
    }
    
    public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority + 1;
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
        
        var filename = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName");
        if (filename == null)
        {
            return result;
        }

        var content = fileSystem.ReadFile(filename);

        var resource = GetDefinedResource(content);
        log.Info($"Deployed: {resource.Kind}/{resource.Name}");

        log.Info($"Status:");
        var n = 5;
        while (!CheckStatus(resource, commandLineRunner) && --n >= 0)
        {
            Thread.Sleep(2000);
        }

        return result;
    }

    private bool CheckStatus(KubernetesResource resource, ICommandLineRunner commandLineRunner)
    {
        var result = ExecuteCommandAndReturnOutput("kubectl",
            new[] {"get", resource.Kind, resource.Name, $"-n {resource.Namespace}"}, commandLineRunner);

        foreach (var line in result)
        {
            log.Info(line);
        }
         
        log.Info("");
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
        var name = resource.SelectToken("$.metadata.name");
        var @namespace = resource.SelectToken("$.metadata.namespace");
        var kind = resource.SelectToken("$.kind");
        return new KubernetesResource
        {
            Name = name.ToString(), Kind = kind.ToString(), Namespace = @namespace.ToString()
        };
    } 
    
    IEnumerable<string> ExecuteCommandAndReturnOutput(string exe, string[] arguments, ICommandLineRunner commandLineRunner)
    {
        var captureCommandOutput = new CaptureCommandOutput();
        var invocation = new CommandLineInvocation(exe, arguments)
        {
            OutputAsVerbose = false,
            OutputToLog = false,
            AdditionalInvocationOutputSink = captureCommandOutput
        };

        commandLineRunner.Execute(invocation);

        return captureCommandOutput.Messages.Where(m => m.Level == Level.Info).Select(m => m.Text).ToArray();

    }
}