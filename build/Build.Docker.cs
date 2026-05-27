using Calamari.Build.Utilities;
using JetBrains.Annotations;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.PowerShell;

namespace Calamari.Build;

public partial class Build
{
    [PublicAPI]
    Target BuildDockerImages =>
        d =>
            d.Executes(() =>
                       {
                           var flavours = GetCalamariFlavours();
                           string[] supportedPlatforms = ["linux/amd64", "linux/arm64", "linux/arm"];
                           var dockerBuildPlatform = string.Join(",", supportedPlatforms);

                           var dockerFile = KnownPaths.DockerDirectory / "Dockerfile";

                           foreach (var flavour in flavours)
                           {
                               Logging.InBlock(flavour, () =>
                                                        {
                                                            var compressedArtifactPath = KnownPaths.OutputsDirectory / $"{flavour}.zip";
                                                            compressedArtifactPath.UncompressTo(KnownPaths.OutputsDirectory / flavour);
                                                            Log.Information("Uncompressed {ZipPath} to {FolderPath}", compressedArtifactPath, KnownPaths.OutputsDirectory / flavour);

                                                            //Rename any `linux-x64` folders to `linux-amd64`
                                                            Directory.Move(KnownPaths.OutputsDirectory / flavour / "linux-x64",
                                                                           KnownPaths.OutputsDirectory / flavour / "linux-amd64");
                                                            Log.Information("Renamed 'linux-x64' folder to 'linux-amd64'");

                                                            var tag = $"octopusdeploy/{flavour}:{NugetVersion.Value}".ToLowerInvariant();

                                                            //build the docker image for this flavour
                                                            DockerTasks.DockerBuildxBuild(settings =>
                                                                                          {
                                                                                              settings = settings
                                                                                                         .AddBuildArg($"FLAVOUR={flavour}")
                                                                                                         .SetPlatform(dockerBuildPlatform)
                                                                                                         .SetTag(tag)
                                                                                                         .SetFile(dockerFile)
                                                                                                         .SetPath(KnownPaths.RootDirectory)
                                                                                                         .EnableLoad();

                                                                                              return settings;
                                                                                          });

                                                            var sanitizedTag = tag.Replace("/", "-").Replace(":", ".");
                                                            var outputFile = KnownPaths.PublishDirectory / $"{sanitizedTag}.tar";

                                                            //create the publish directory
                                                            if (!Directory.Exists(KnownPaths.PublishDirectory))
                                                            {
                                                                Directory.CreateDirectory(KnownPaths.PublishDirectory);
                                                            }

                                                            //save the docker image to a tar file
                                                            DockerTasks.DockerImageSave(_ => _
                                                                                             .SetImages(tag)
                                                                                             .SetOutput(outputFile));

                                                            var compressedZipPath = $"{outputFile}.gz";

                                                            //compress with gzip
                                                            PowerShellTasks.PowerShell(_ => _.SetCommand($"Compress-Archive -Path '{outputFile}' -DestinationPath '{compressedZipPath}'").EnableNoProfile());

                                                            // This file is then uploaded to OctopusDeploy to perform the release process
                                                            if (TeamCity.Instance is not null)
                                                            {
                                                                TeamCity.Instance.PublishArtifacts(compressedZipPath);
                                                            }
                                                        });
                           }
                       });
}