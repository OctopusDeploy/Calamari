using Calamari.Build.Utilities;
using JetBrains.Annotations;
using Nuke.Common.Tools.Docker;

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
                                                            Log.Information("Renamed 'linux-x64' folder to 'linux-am64'");

                                                            var tag = $"octopusdeploy/{flavour}:{NugetVersion.Value}".ToLowerInvariant();

                                                            //build the docker image for this flavour
                                                            DockerTasks.DockerBuildxBuild(settings =>
                                                                                          {
                                                                                              settings = settings
                                                                                                         .AddBuildArg($"FLAVOUR={flavour}")
                                                                                                         .SetPlatform(dockerBuildPlatform)
                                                                                                         .SetTag(tag)
                                                                                                         .SetFile(dockerFile)
                                                                                                         .SetPath(KnownPaths.RootDirectory);

                                                                                              return settings;
                                                                                          });

                                                            var sanitizedTag = tag.Replace("/", "-");
                                                            var outputFile = KnownPaths.PublishDirectory / $"{sanitizedTag}.tar";

                                                            //save the docker image to a tar file
                                                            DockerTasks.DockerImageSave(_ => _
                                                                                             .SetImages(tag)
                                                                                             .SetOutput(outputFile));

                                                            //compress with gzip
                                                            var compressedZipPath = $"{outputFile}.gz";
                                                            outputFile.CompressTo(compressedZipPath);

                                                            // This file is then uploaded to OctopusDeploy to perform the release process
                                                            if (TeamCity.Instance is not null)
                                                            {
                                                                TeamCity.Instance.PublishArtifacts(compressedZipPath);
                                                            }
                                                        });
                           }
                       });
}