using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.OctoVersion;
using Calamari.Build.Utilities;
using Nuke.Common.IO;
using Serilog;

namespace Calamari.Build;

partial class Build
{
    [ParameterFromPasswordStore(Name = "DEPENDENCY_TRACK_URL", SecretReference = "op://Calamari Secrets for Tests/Dependency Track SBOM API/hostname")] 
    readonly string? DependencyTrackUrl;
    [ParameterFromPasswordStore(Name = "DEPENDENCY_TRACK_API_KEY", SecretReference = "op://Calamari Secrets for Tests/Dependency Track SBOM API/credential"), Secret] 
    readonly string? DependencyTrackApiKey;
    
    readonly List<string> ContainersWeHaveCreated = new();
    
    // ReSharper disable InconsistentNaming
    [PublicAPI("Called by TeamCity")]
    public Target BuildSoftwareBillOfMaterials => _ => _
        .Requires(() => DependencyTrackUrl)
        .Requires(() => DependencyTrackApiKey)
        .DependsOn(Publish)
        .Executes(async () =>
        {
            ArgumentNullException.ThrowIfNull(Solution, nameof(Solution));
            var octoVersionInfo = OctoVersionInfo.Value ?? throw new InvalidOperationException("Required OctoVersionInfo was not populated");
            var combinedFileName = $"calamari.{octoVersionInfo.FullSemVer}-sbom.cdx.json";

            // redirect all docker output to stdout, as lots of it goes as stderr when it's just progress messages
            DockerTasks.DockerLogger = (_, message) => Log.Information("[Docker] {Message}", message);
            
            EnsureDockerImagesExistLocally();

            var results = new List<string>();
            Logging.InBlock("Creating individual SBOMs", () =>
            {
                ArtifactsDirectory.CreateOrCleanDirectory();
                var components = Directory
                    .EnumerateFiles(RootDirectory, "*.deps.json", SearchOption.AllDirectories)
                    .Where(path => !path.Contains("/obj/"))
                    .Where(path => !path.Contains("/TestResults/"))
                    .Where(path => !path.Contains("/.git/"))
                    .Where(path => !path.Contains(".Test"))
                    .Where(path => !path.Contains("/_build"))
                    .Select(ResolveCalamariComponent);

                foreach (var component in components)
                {
                    var sbomFile = CreateSBOM(component.Directory, component.Project, component.Framework, octoVersionInfo.FullSemVer, component.Runtime);
                    results.Add(sbomFile);
                }

            });
            CombineAndValidateSBOM(octoVersionInfo, results.Select(fileName => $"/sboms/{fileName}").ToArray(), combinedFileName);
            await UploadToDependencyTrack(octoVersionInfo, combinedFileName);
        });

    static CalamariComponent ResolveCalamariComponent(string x)
    {
        var runtimeTarget = RuntimeTargetParser.ParseFromFile(x);

        return new CalamariComponent(
            Directory: Path.GetDirectoryName(x) ?? throw new InvalidOperationException($"Could not determine directory name for '{x}'"),
            Project: Path.GetFileName(x).Replace(".deps.json", ""),
            Framework: runtimeTarget.Framework,
            Runtime: runtimeTarget.Runtime
        );
    }
    

    static void EnsureDockerImagesExistLocally()
    {
        Logging.InBlock($"Ensuring trivy container exists locally", () =>
        {
            DockerTasks.DockerPull(x => x
                .SetName("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-sbom-cli:latest")
                .SetPlatform("linux/amd64"));
        });

        Logging.InBlock($"Ensuring cyclonedx/cyclonedx-cli container exists locally", () =>
        {
            DockerTasks.DockerPull(x => x
                .SetName("cyclonedx/cyclonedx-cli:latest")
                .SetPlatform("linux/amd64"));
        });
    }

    async Task UploadToDependencyTrack(OctoVersionInfo octoVersionInfo, string fileName)
    {
        await Logging.InBlock($"Uploading SBOM to Dependency Track", () =>
        {
            if (string.IsNullOrWhiteSpace(DependencyTrackUrl) || string.IsNullOrWhiteSpace(DependencyTrackApiKey))
            {
                Log.Warning("Skipping upload to Dependency Track as DEPENDENCY_TRACK_URL and DEPENDENCY_TRACK_API_KEY are not set");
                return Task.CompletedTask;
            }

            var parentName = $"Calamari-{octoVersionInfo.Major}.{octoVersionInfo.Minor}";
            var version = octoVersionInfo.FullSemVer;
            var containerName = $"calamari-sbom-uploader-{version}";
            var projectName = "Calamari";

            var args = new List<string>();
            if (BranchName != null && (BranchName is "refs/heads/main" || BranchName.StartsWith("refs/heads/release/")))
            {
                args.Add("--latest");
            }
            args.Add("--sbom");
            args.Add($"/sboms/{fileName}");
            ContainersWeHaveCreated.Add(containerName);
            
            DockerTasks.DockerRun(x => x
                .SetName(containerName)
                .SetPlatform("linux/amd64")
                .SetImage("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-sbom-cli:latest")
                .SetVolume($"{ArtifactsDirectory}:/sboms")
                .SetCommand($"sbom-uploader")
                .SetEnv(
                        $"SBOM_UPLOADER_URL={DependencyTrackUrl}",
                        $"SBOM_UPLOADER_API_KEY={DependencyTrackApiKey}", 
                        $"SBOM_UPLOADER_NAME={projectName}", 
                        $"SBOM_UPLOADER_VERSION={octoVersionInfo.FullSemVer}",
                        $"SBOM_UPLOADER_PARENT={parentName}",
                        $"SBOM_UPLOADER_TAGS={projectName},{parentName}")
                .SetArgs(args)
                .SetRm(true));
            
            return Task.CompletedTask;
        });
    }

    /// <returns>the created SBOM filename</returns>
    string CreateSBOM(string directory, string project, string framework, string version, string? runtimeId)
    {
        return Logging.InBlock($"Creating SBOM for project '{project}', framework '{framework}' and runtime '{runtimeId}'", () =>
             {
                 var containerName = $"calamari-sbom-{project}-{framework}{runtimeId}";
                 Log.Information("Creating SBOM for {Project} from {Directory}/", project, directory);

                 var runtimeSuffix = GetRuntimeSuffix(runtimeId);

                 var outputFile = $"{project}.{version}{runtimeSuffix}.sbom.cdx.json";
                 ContainersWeHaveCreated.Add(containerName);
                 DockerTasks.DockerRun(x => x
                                            .SetName(containerName)
                                            .SetPlatform("linux/amd64")
                                            .SetImage("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-sbom-cli:latest")
                                            .SetVolume($"{directory}:/source", $"{ArtifactsDirectory}:/output")
                                            .SetCommand($"trivy")
                                            .SetArgs("fs", $"/source", "--format", "cyclonedx",
                                                     "--output", $"/output/{outputFile}")
                                            .SetRm(true));

                 TeamCity.Instance?.PublishArtifacts(ArtifactsDirectory / outputFile);

                 return outputFile;
             });
    }

    static string GetRuntimeSuffix(string? runtimeId)
    {
        if (runtimeId is null or "portable" or "Cloud")
            return "";
        return $".{runtimeId}";
    }

    void CombineAndValidateSBOM(OctoVersionInfo octoVersionInfo, string[] inputFiles, string outputFileName)
    {
        Logging.InBlock($"Combining SBOMs", () =>
        {
            var containerName = $"calamari-sbom-combiner-{octoVersionInfo.FullSemVer}";
            ContainersWeHaveCreated.Add(containerName);
                
            var args = new [] {"--input-files"}
                .Concat(inputFiles)
                .Concat(new[] {"--output-file", $"/sboms/{outputFileName}"})
                .ToArray();
            DockerTasks.DockerRun(x => x
                .SetName(containerName)
                .SetPlatform("linux/amd64")
                .SetRm(true)
                .SetVolume($"{ArtifactsDirectory}:/sboms")
                .SetImage("cyclonedx/cyclonedx-cli")
                .SetCommand("merge")
                .SetArgs(args));
            TeamCity.Instance?.PublishArtifacts(ArtifactsDirectory / outputFileName);
        });

        Logging.InBlock($"Validating combined SBOM", () =>
        {
            var containerName = $"calamari-sbom-validator-{octoVersionInfo.FullSemVer}";
            ContainersWeHaveCreated.Add(containerName);
            DockerTasks.DockerRun(x => x
                .SetName($"octopus-sbom-validator-{octoVersionInfo.FullSemVer}")
                .SetPlatform("linux/amd64")
                .SetRm(true)
                .SetVolume($"{ArtifactsDirectory}:/sboms")
                .SetImage("cyclonedx/cyclonedx-cli")
                .SetCommand("validate")
                .SetArgs("--input-file", $"/sboms/{outputFileName}"));
        });
    
    }

    [PublicAPI("Automatically runs after BuildSoftwareBillOfMaterials")]
    public Target CleanUpSoftwareBillOfMaterials => _ => _
        .TriggeredBy(BuildSoftwareBillOfMaterials)
        .After(BuildSoftwareBillOfMaterials)
        .AssuredAfterFailure()
        .Unlisted()
        .Executes(() =>
        {
            foreach(var containerName in ContainersWeHaveCreated)
            {
                DockerTasks.DockerContainerRm(t => t
                    .SetContainers(containerName)
                    .SetForce(true));
            }
        });
}

record CalamariComponent(string Directory, string Project, string Framework, string? Runtime);
