using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Conventions
{
    public class GatherAndApplyRawYamlConvention: IInstallConvention
    {
        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ICommandLineRunner commandLineRunner;

        public GatherAndApplyRawYamlConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var globs = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName")?.Split(';');
            if (globs == null || globs.All(g => g.IsNullOrEmpty()))
                return;

            var directories = GroupFilesIntoDirectories(deployment, globs, variables);

            var kubectl = GetAndInitialiseKubectl(deployment, variables);

            var resources = new List<Resource>();
            foreach (var (directory, index) in directories.Select((d,i) => (d,i)))
            {
                resources.AddRange(ApplyBatchAndReturnResources(index, kubectl, directory));
            }

            SaveResourcesToOutputVariables(resources, kubectl, variables);
        }

        private Kubectl GetAndInitialiseKubectl(RunningDeployment deployment, IVariables variables)
        {
            var kubectl = new Kubectl(variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable"), log,
                commandLineRunner, deployment.CurrentDirectory, deployment.EnvironmentVariables);

            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Could not set KubeCtl");
            }

            return kubectl;
        }

        private void SaveResourcesToOutputVariables(List<Resource> resources, Kubectl kubectl, IVariables variables)
        {
            foreach (var resource in resources)
            {
                try
                {
                    var result = kubectl.ExecuteCommandAndReturnOutput("get", resource.Kind, resource.Metadata.Name,
                        "-o", "json");
                    variables.SetOutputVariable($"CustomResources({resource.Metadata.Name})",
                        result.Output.InfoLogs.Join("\n"));
                }
                catch
                {
                    log.Warn(
                        $"Could not save json for resource to output variable for '{resource.Kind}/{resource.Metadata.Name}'");
                }
            }
        }

        private List<string> GroupFilesIntoDirectories(RunningDeployment deployment, string[] globs, IVariables variables)
        {
            var directories = new List<string>();
            foreach (var (glob, idx) in globs.Select((g, i) => (g, i)))
            {
                var files = Directory.GetFiles(deployment.CurrentDirectory, glob);
                var directory = Path.Combine(deployment.CurrentDirectory, "grouped", idx.ToString());
                Directory.CreateDirectory(directory);
                foreach (var file in files)
                {
                    fileSystem.CopyFile(file, Path.Combine(directory, Path.GetFileName(file)));
                }

                directories.Add(directory);
            }

            variables.Set("Octopus.Action.KubernetesContainers.YamlDirectories", string.Join(";", directories));
            return directories;
        }

        private IEnumerable<Resource> ApplyBatchAndReturnResources(int index, Kubectl kubectl, string directory)
        {
            log.Verbose($"Applying Yaml Batch #{index}");
            var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", directory, "-o", "json");

            foreach (var message in result.Output.Messages)
            {
                switch (message.Level)
                {
                    case Level.Info:
                        //No need to log as it's the output json from above.
                        break;
                    case Level.Error:
                        log.Error(message.Text);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (result.Result.ExitCode != 0)
            {
                throw new Exception(result.Result.Errors);
            }

            // If it did not error, the output should be valid json.
            var outputJson = result.Output.InfoLogs.Join(Environment.NewLine);
            try
            {
                var token = JToken.Parse(outputJson);
                var lastResources = token.Type == JTokenType.Array
                    ? token.ToObject<List<Resource>>()
                    : new List<Resource> { token.ToObject<Resource>() };

                foreach (var resource in lastResources)
                {
                    log.Info($"'{resource.Kind}/{resource.Metadata.Name}' created.");
                }

                return lastResources;
            }
            catch
            {
                log.Error($"\"kubectl apply -o json\" returned invalid JSON for Batch #{index}:");
                log.Error("---------------------------");
                log.Error(outputJson);
                log.Error("---------------------------");
                log.Error("This can happen with older versions of kubectl. Please update to a recent version of kubectl.");
                log.Error("See https://github.com/kubernetes/kubernetes/issues/58834 for more details.");
                log.Error("Custom resources will not be saved as output variables.");
            }

            return Enumerable.Empty<Resource>();
        }

        private class ResourceMetadata
        {
            public string Name { get; set; }
        }

        private class Resource
        {
            public string Kind { get; set; }
            public ResourceMetadata Metadata { get; set; }
        }
    }
}