namespace Calamari.Build;

public partial class Build
{
    Target PublishAzureWebAppNetCoreShim =>
        _ => _.DependsOn(RestoreSolution)
              .DependsOn(GetCalamariFlavourProjectsToPublish)
              //we only build the net core shim when there is the AzureWebApp project is being built
              .OnlyWhenDynamic(() => CalamariProjects.Any(p => p.Name == "Calamari.AzureWebApp"))
              .Executes(() =>
                        {
                            if (!OperatingSystem.IsWindows())
                            {
                                Log.Warning("Unable to build Calamari.AzureWebApp.NetCoreShim as it's a Full Framework application and can only be compiled on Windows");
                                return;
                            }

                            var project = Solution.GetProject("Calamari.AzureWebApp.NetCoreShim");
                            if (project is null)
                            {
                                Log.Error("Failed to find Calamari.AzureWebApp.NetCoreShim project");
                                return;
                            }

                            var outputPath = KnownPaths.PublishDirectory / project.Name;

                            //as this is the only Net 4.6.2 application left, we do a build and restore here
                            DotNetPublish(s => s
                                               .SetConfiguration(Configuration)
                                               .SetProject(project.Path)
                                               .SetFramework("net462")
                                               .SetVersion(NugetVersion.Value)
                                               .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                               .SetOutput(outputPath));

                            var archivePath = KnownPaths.SourceDirectory / "Calamari.AzureWebApp" / "netcoreshim" / "netcoreshim.zip";
                            archivePath.DeleteFile();

                            outputPath.CompressTo(archivePath);
                        });
}