#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IGatherAndApplyRawYamlExecutor
    {
        Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null);
    }

    public class GatherAndApplyRawYamlExecutor : IGatherAndApplyRawYamlExecutor
    {
        private const string GroupedDirectoryName = "grouped";

        private readonly ILog log;
        private readonly ICalamariFileSystem fileSystem;
        private readonly Kubectl kubectl;

        public GatherAndApplyRawYamlExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            Kubectl kubectl)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.kubectl = kubectl;
        }

        public async Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback)
        {
            try
            {
                var variables = deployment.Variables;
                var globs = variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);
                if (globs.IsNullOrEmpty())
                    return true;
                var directories = GroupFilesIntoDirectories(deployment, globs, variables);
                var resources = new HashSet<Resource>();
                foreach (var directory in directories)
                {
                    var res = ApplyBatchAndReturnResources(directory).ToList();
                    if (appliedResourcesCallback != null)
                    {
                        await appliedResourcesCallback(res.Select(r => r.ToResourceIdentifier()).ToArray());
                    }
                    resources.UnionWith(res);
                }

                WriteResourcesToOutputVariables(resources);
                return true;
            }
            catch (GatherAndApplyRawYamlException)
            {
                return false;
            }
            catch (Exception e)
            {
                log.Error($"Deployment Failed due to exception: {e.Message}");
                return false;
            }
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

        private IEnumerable<GlobDirectory> GroupFilesIntoDirectories(RunningDeployment deployment, List<string> globs, IVariables variables)
        {
            var stagingDirectory = deployment.CurrentDirectory;
            var packageDirectory =
                Path.Combine(stagingDirectory, KubernetesDeploymentCommandBase.PackageDirectoryName) +
                Path.DirectorySeparatorChar;

            var directories = new List<GlobDirectory>();
            for (var i = 1; i <= globs.Count; i ++)
            {
                var glob = globs[i-1];
                var directoryPath = Path.Combine(stagingDirectory, GroupedDirectoryName, i.ToString());
                var directory = new GlobDirectory(i, glob, directoryPath);
                fileSystem.CreateDirectory(directoryPath);

                var globMode = GlobModeRetriever.GetFromVariables(variables);
                var results = fileSystem.EnumerateFilesWithGlob(packageDirectory, globMode, glob);
                foreach (var file in results)
                {
                    var relativeFilePath = fileSystem.GetRelativePath(packageDirectory, file);
                    var targetPath = Path.Combine(directoryPath, relativeFilePath);
                    var targetDirectory = Path.GetDirectoryName(targetPath);
                    if (targetDirectory != null)
                    {
                        fileSystem.CreateDirectory(targetDirectory);
                    }
                    fileSystem.CopyFile(file, targetPath);
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
            var directory = globDirectory.Directory + Path.DirectorySeparatorChar;
            log.Info($"Applying Batch #{index} for YAML matching '{globDirectory.Glob}'");

            var files = fileSystem.EnumerateFilesRecursively(globDirectory.Directory).ToArray();
            if (!files.Any())
            {
                log.Warn($"No files found matching '{globDirectory.Glob}'");
                return Enumerable.Empty<Resource>();
            }

            foreach (var file in files)
            {
                log.Verbose($"Matched file: {fileSystem.GetRelativePath(directory, file)}");
            }

            var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", $"'{directory}'", "--recursive", "-o", "json");

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
                        log.Error(message.Text.Replace($"{directory}", ""));
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

                var resources = lastResources.Select(r => r.ToResourceIdentifier()).ToList();

                if (resources.Any())
                {
                    log.Verbose("Created Resources:");
                    log.LogResources(resources);
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

            throw new GatherAndApplyRawYamlException();
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

        private class GatherAndApplyRawYamlException : Exception
        {
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