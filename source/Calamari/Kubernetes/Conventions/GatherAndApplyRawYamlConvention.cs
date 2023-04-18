using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;

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
            var rawGlobs = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName");
            if (rawGlobs == null)
                return;

            var globs = rawGlobs.Split(';');
            var directories = new List<string>();
            foreach (var (glob, idx) in globs.Select((g,i) => (g,i)))
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

            var kubectl = new Kubectl(variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable"), log,
                commandLineRunner, deployment.CurrentDirectory, deployment.EnvironmentVariables);

            if (!kubectl.TrySetKubectl())
            {
                throw new Exception("Could not set KubeCtl");
            }

            foreach (var (directory, index) in directories.Select((d,i) => (d,i)))
            {
                log.Verbose($"Applying Yaml Batch #{index}");
                var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", directory, "-o", "json");
                foreach (var message in result.Output.Messages)
                {
                    switch (message.Level)
                    {
                        case Level.Info:
                            log.Verbose(message.Text);
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
            }
        }
    }
}