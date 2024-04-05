#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IRawYamlKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
    }
    
    class GatherAndApplyRawYamlExecutor : BaseKubernetesApplyExecutor, IRawYamlKubernetesApplyExecutor
    {
        const string GroupedDirectoryName = "grouped";

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly Kubectl kubectl;

        public GatherAndApplyRawYamlExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            Kubectl kubectl) : base(log, kubectl)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.kubectl = kubectl;
        }

        protected override async Task<IEnumerable<ResourceIdentifier>> ApplyAndGetResourceIdentifiers(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
        {
            var variables = deployment.Variables;
            var globs = variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);
            
            if (globs.IsNullOrEmpty())
                return Enumerable.Empty<ResourceIdentifier>();
            
            var directories = GroupFilesIntoDirectories(deployment, globs, variables);
            
            var resourcesIdentifiers = new HashSet<ResourceIdentifier>();
            foreach (var directory in directories)
            {
                var res = ApplyBatchAndReturnResourceIdentifiers(deployment, directory).ToArray();

                if (appliedResourcesCallback != null)
                {
                    await appliedResourcesCallback(res);
                }
                
                resourcesIdentifiers.UnionWith(res);
            }

            return resourcesIdentifiers;
        }

        IEnumerable<GlobDirectory> GroupFilesIntoDirectories(RunningDeployment deployment, List<string> globs, IVariables variables)
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

        IEnumerable<ResourceIdentifier> ApplyBatchAndReturnResourceIdentifiers(RunningDeployment deployment, GlobDirectory globDirectory)
        {
            var index = globDirectory.Index;
            var directoryWithTrailingSlash = globDirectory.Directory + Path.DirectorySeparatorChar;
            log.Info($"Applying Batch #{index} for YAML matching '{globDirectory.Glob}'");

            var files = fileSystem.EnumerateFilesRecursively(globDirectory.Directory).ToArray();
            if (!files.Any())
            {
                log.Warn($"No files found matching '{globDirectory.Glob}'");
                return Enumerable.Empty<ResourceIdentifier>();
            }

            foreach (var file in files)
            {
                log.Verbose($"Matched file: {fileSystem.GetRelativePath(directoryWithTrailingSlash, file)}");
            }

            var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", $@"""{globDirectory.Directory}""", "--recursive", "-o", "json");

            return ProcessKubectlCommandOutput(deployment, result, globDirectory.Directory);
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