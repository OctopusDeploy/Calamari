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
using Nuke.Common.Tooling;
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

            EnsureDockerImagesExistLocally();

            var results = new List<string>();
            Logging.InBlock("Creating SBOM", () =>
             {
                 //not worrying about .NET Framework for SBOMs right now - we're hoping to drop it soon.
                 //var framework = OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60;
                 //results.Add(CreateSBOM(RootProjectName, framework, NugetVersion.Value, FixedRuntimes.Cloud));

                 foreach (var rid in GetRuntimeIdentifiers(Solution.GetProject(RootProjectName)!))
                     results.Add(CreateSBOM(RootProjectName, Frameworks.Net60, NugetVersion.Value, rid));

                 CombineAndValidateSBOM(octoVersionInfo, results.Select(fileName => $"/sboms/{fileName}").ToArray(), combinedFileName);
             });
            await UploadToDependencyTrack(octoVersionInfo, combinedFileName);
        });

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
    string CreateSBOM(string project, string framework, string version, string? runtimeId = null)
    {
        var containerName = $"calamari-sbom-{project}-{framework}{runtimeId}";
        var publishedTo = GetPublishedTo(project, framework, runtimeId);

        var runtimeSuffix = GetRuntimeSuffix(runtimeId);
        
        var outputFile = $"{project}.{version}{runtimeSuffix}.sbom.cdx.json";
        ContainersWeHaveCreated.Add(containerName);
        DockerTasks.DockerRun(x => x
           .SetName(containerName)
           .SetPlatform("linux/amd64")
           .SetImage("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-sbom-cli:latest")
           .SetVolume($"{SourceDirectory}:/source", $"{ArtifactsDirectory}:/output")
           .SetCommand($"trivy")
           .SetArgs("fs", $"/source/{publishedTo}", "--format", "cyclonedx",
                    "--output", $"/output/{outputFile}")
           .SetRm(true));

        TeamCity.Instance?.PublishArtifacts(ArtifactsDirectory / outputFile);
        
        return outputFile;
    }

    static string GetRuntimeSuffix(string? runtimeId)
    {
        if (runtimeId is null or "portable" or "Cloud")
            return "";
        return $".{runtimeId}";
    }

    string GetPublishedTo(string project, string framework, string? runtimeId)
    {
        if (runtimeId != null && File.Exists(SourceDirectory / project / "bin" / Configuration / framework / runtimeId / $"{project}.deps.json"))
            return $"{project}/bin/{Configuration}/{framework}/{runtimeId}";

        if (File.Exists(SourceDirectory / project / "bin" / Configuration / framework / $"{project}.deps.json"))
            return $"{project}/bin/{Configuration}/{framework}";

        if (File.Exists(SourceDirectory / project / runtimeId / $"{project}.deps.json"))
            return $"{project}/{runtimeId}"; 

        throw new DirectoryNotFoundException($"Could not find published output for projectg '{project}', framework '{framework}' and runtime '{framework} in {SourceDirectory} - unable to create SBOM");
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
