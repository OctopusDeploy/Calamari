using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Conventions;

public class GatherAndApplyRawYamlConvention: IInstallConvention
{
    private readonly ILog log;
    private readonly IVariables variables;
    private readonly ICalamariFileSystem fileSystem;
    private readonly ICommandLineRunner commandLineRunner;

    public GatherAndApplyRawYamlConvention(
        ILog log,
        IVariables variables,
        ICalamariFileSystem fileSystem,
        ICommandLineRunner commandLineRunner)
    {
        this.log = log;
        this.variables = variables;
        this.fileSystem = fileSystem;
        this.commandLineRunner = commandLineRunner;
    }

    public void Install(RunningDeployment deployment)
    {
        var rawGlobs = variables.Get("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName");
        if (rawGlobs == null)
            return;

        var globs = rawGlobs.Split(";");
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
            commandLineRunner, deployment.CurrentDirectory, new Dictionary<string, string>());

        foreach (var directory in directories)
        {
            var output = kubectl.ExecuteCommandAndReturnOutput("apply", "-f", directory, "-o", "json");
        }
    }
}