#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Conventions
{
    public class GatherAndApplyRawYamlConvention: IInstallConvention
    {
        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly Kubectl kubectl;
        private readonly KubectlResourcesAppliedEvent resourcesAppliedEvent;

        public GatherAndApplyRawYamlConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            Kubectl kubectl,
            KubectlResourcesAppliedEvent resourcesAppliedEvent)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.kubectl = kubectl;
            this.resourcesAppliedEvent = resourcesAppliedEvent;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var globs = variables.Get(SpecialVariables.CustomResourceYamlFileName)?.Split(';');
            if (globs == null || globs.All(g => g.IsNullOrEmpty()))
                return;

            var directories = GroupFilesIntoDirectories(deployment, globs, variables);

            var resources = directories.SelectMany(d =>
            {
                var res = ApplyBatchAndReturnResources(d).ToList();
				resourcesAppliedEvent.Publish(res.Select(r => r.ToResourceIdentifier()).ToArray());
				return res;
			});

            WriteResourcesToOutputVariables(resources);
        }

        private void WriteResourcesToOutputVariables(IEnumerable<Resource> resources)
        {
            foreach (var resource in resources)
            {
                try
                {
                    var result = kubectl.ExecuteCommandAndReturnOutput("get", resource.Kind, resource.Metadata.Name,
                        "-o", "json");

                    log.WriteServiceMessage(new ServiceMessage(ServiceMessageNames.SetVariable.Name, new Dictionary<string, string>
                    {
                        {ServiceMessageNames.SetVariable.NameAttribute, $"CustomResources({resource.Metadata.Name})"},
                        {ServiceMessageNames.SetVariable.ValueAttribute, result.Output.InfoLogs.Join("\n")}
                    }));
                }
                catch
                {
                    log.Warn(
                        $"Could not save json for resource to output variable for '{resource.Kind}/{resource.Metadata.Name}'");
                }
            }
        }

        private IEnumerable<GlobDirectory> GroupFilesIntoDirectories(RunningDeployment deployment, string[] globs, IVariables variables)
        {
            var directories = new List<GlobDirectory>();
            for (var i = 0; i < globs.Length; i ++)
            {
                var glob = globs[i];
                var directoryPath = Path.Combine(deployment.CurrentDirectory, "grouped", i.ToString());
                var directory = new GlobDirectory(i, glob, directoryPath);
                fileSystem.CreateDirectory(directoryPath);

                var matcher = new Matcher();
                matcher.AddInclude(glob);
                var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(deployment.CurrentDirectory)));
                foreach (var file in result.Files)
                {
                    var relativeFilePath = file.Path ?? file.Stem;
                    var fullPath = Path.Combine(deployment.CurrentDirectory, relativeFilePath);
                    var targetPath = Path.Combine(directoryPath, relativeFilePath);
                    var targetDirectory = Path.GetDirectoryName(targetPath);
                    if (targetDirectory != null)
                    {
                        fileSystem.CreateDirectory(targetDirectory);
                    }
                    fileSystem.CopyFile(fullPath, targetPath);
                }

                directories.Add(directory);
            }

            variables.Set(SpecialVariables.GroupedYamlDirectories,
                string.Join(";", directories.Select(d => d.Directory)));
            return directories;
        }

        private IEnumerable<Resource> ApplyBatchAndReturnResources(GlobDirectory globDirectory)
        {
            var index = globDirectory.Index;
            var directory = globDirectory.Directory;
            log.Info($"Applying Batch #{index+1} for YAML matching '{globDirectory.Glob}'");
            foreach (var file in fileSystem.EnumerateFilesRecursively(globDirectory.Directory))
            {
                // TODO: Once we have moved to a higher .net Framework version, update fileSystem.GetRelativePath to use
                // Path.GetRelativePath() instead of our own implementation, and update the code below to remove the
                // usage of Path.DirectorySeparatorChar.
                log.Verbose($"{fileSystem.GetRelativePath($"{directory}{Path.DirectorySeparatorChar}", file)} Contents:");
                log.Verbose(fileSystem.ReadFile(file));
            }
            var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", directory, "--recursive", "-o", "json");

            foreach (var message in result.Output.Messages)
            {
                switch (message.Level)
                {
                    case Level.Info:
                        //No need to log as it's the output json from above.
                        break;
                    case Level.Error:
                        //Files in the error are shown with the full path in their batch directory,
                        //so we'll remove that for the user.
                        log.Error(message.Text.Replace($"{directory}{Path.DirectorySeparatorChar}", ""));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (result.Result.ExitCode != 0)
            {
                LogParsingError(null, index);
            }

            // If it did not error, the output should be valid json.
            var outputJson = result.Output.InfoLogs.Join(Environment.NewLine);
            try
            {
                var token = JToken.Parse(outputJson);

                List<Resource> lastResources;
                if (token["kind"]?.ToString() != "List" ||
                    (lastResources = token["items"]?.ToObject<List<Resource>>()) == null)
                {
                    lastResources = new List<Resource> { token.ToObject<Resource>() };
                }

                foreach (var resource in lastResources)
                {
                    log.Info($"{resource.Kind}/{resource.Metadata.Name} created");
                }

                return lastResources;
            }
            catch
            {
                LogParsingError(outputJson, index);
            }

            return Enumerable.Empty<Resource>();
        }

        private void LogParsingError(string outputJson, int index)
        {
            log.Error($"\"kubectl apply -o json\" returned invalid JSON for Batch #{index}:");
            log.Error("---------------------------");
            log.Error(outputJson);
            log.Error("---------------------------");
            log.Error("This can happen with older versions of kubectl. Please update to a recent version of kubectl.");
            log.Error("See https://github.com/kubernetes/kubernetes/issues/58834 for more details.");
            log.Error("Custom resources will not be saved as output variables.");

            throw new KubernetesDeploymentFailedException();
        }

        private class ResourceMetadata
        {
            public string Namespace { get; set; }
            public string Name { get; set; }
        }

        private class Resource
        {
            public string Kind { get; set; }
            public ResourceMetadata Metadata { get; set; }

            public ResourceIdentifier ToResourceIdentifier()
            {
                return new ResourceIdentifier(Kind, Metadata.Name, Metadata.Namespace);
            }
        }

        private class GlobDirectory
        {
            public GlobDirectory(int index, string glob, string directory)
            {
                Index = index;
                Glob = glob;
                Directory = directory;
            }

            public int Index { get; }
            public string Glob { get; }
            public string Directory { get; }
        }
    }
}
#endif