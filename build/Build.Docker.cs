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
                           if (OperatingSystem.IsWindows())
                           {
                               Log.Error("The Docker images can not be built on Windows");
                               return;
                           }

                           var flavours = GetCalamariFlavours();
                           string[] supportedPlatforms = ["linux/amd64", "linux/arm64", "linux/arm"];
                           var dockerBuildPlatform = string.Join(",", supportedPlatforms);

                           var dockerFile = KnownPaths.DockerDirectory / "Dockerfile";

                           foreach (var flavour in flavours)
                           {
                               Logging.InBlock(flavour, () =>
                                                        {
                                                            var flavourFolder = KnownPaths.OutputsDirectory / flavour;

                                                            var compressedArtifactPath = KnownPaths.OutputsDirectory / $"{flavour}.zip";
                                                            compressedArtifactPath.UncompressTo(flavourFolder);
                                                            Log.Information("Uncompressed {ZipPath} to {FolderPath}", compressedArtifactPath, flavourFolder);

                                                            //Rename any `linux-x64` folders to `linux-amd64`
                                                            Directory.Move(flavourFolder / "linux-x64",
                                                                flavourFolder / "linux-amd64");
                                                            Log.Information("Renamed 'linux-x64' folder to 'linux-amd64'");

                                                            string[] binariesToChmodX = ["docker-credential-octopus", flavour];

                                                            foreach (var supportedPlatform in supportedPlatforms)
                                                            {
                                                                var platformFolder = supportedPlatform.Replace("/", "-");
                                                                
                                                                // change the native binaries to be executable in each platform
                                                                foreach (var binary in binariesToChmodX)
                                                                {
                                                                    string binaryPath = flavourFolder / platformFolder / binary;
                                                                    
                                                                    //Not all binaries exist in all flavours (The extra win check skips the CA1416 issues)
                                                                    if (File.Exists(binaryPath) && !OperatingSystem.IsWindows())
                                                                    {
                                                                        var mode = File.GetUnixFileMode(binaryPath);
                                                                        File.SetUnixFileMode(binaryPath, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                                                                    }
                                                                }
                                                            }

                                                            var tag = $"octopusdeploy/{flavour}:{NugetVersion.Value}".ToLowerInvariant();
                                                            
                                                            var sanitizedTag = tag.Replace("/", "-").Replace(":", ".");
                                                            var outputFile = KnownPaths.PublishDirectory / $"{sanitizedTag}.tar";

                                                            //create the publish directory
                                                            Directory.CreateDirectory(KnownPaths.PublishDirectory);
                                                            

                                                            //build the docker image for this flavour
                                                            DockerTasks.DockerBuildxBuild(settings =>
                                                                                          {
                                                                                              settings = settings
                                                                                                         .AddBuildArg($"FLAVOUR={flavour}")
                                                                                                         .SetPlatform(dockerBuildPlatform)
                                                                                                         .SetTag(tag)
                                                                                                         .SetFile(dockerFile)
                                                                                                         .SetPath(KnownPaths.RootDirectory)
                                                                                                         .SetOutput($"type=tar,dest={outputFile}");
                                                                                                         // This is required so we can save below.
                                                                                                         // Otherwise, the image just remains in the build cache
                                                                                                         //.EnableLoad();

                                                                                              return settings;
                                                                                          });



                                                            //save the docker image to a tar file
                                                            // DockerTasks.DockerImageSave(_ => _
                                                            //                                  .SetImages(tag)
                                                            //                                  .SetOutput(outputFile));

                                                            //compress with gzip
                                                            PowerShellTasks.PowerShell(_ => _
                                                                                            .EnableNoProfile()
                                                                                            .SetCommand($"gzip -k -9 -f '{outputFile}'"));

                                                            //gzip always uses the .gz suffix
                                                            var compressedZipPath = $"{outputFile}.gz";

                                                            // This file is then uploaded to OctopusDeploy to perform the release process
                                                            TeamCity.Instance?.PublishArtifacts(compressedZipPath);
                                                        });
                           }
                       });
}