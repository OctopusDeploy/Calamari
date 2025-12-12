using System;
using Calamari.Build.Utilities;
using JetBrains.Annotations;

namespace Calamari.Build;

public partial class Build
{
    [PublicAPI]
    Target PublishAzureWebAppNetCoreShim =>
        _ => _.DependsOn(Clean)
              .Executes(() =>
                        {
                            if (!OperatingSystem.IsWindows())
                            {
                                Log.Warning("Unable to build Calamari.AzureWebApp.NetCoreShim as it's a .NET 4.6.2 application and can only be compiled on Windows");
                                return;
                            }

                            var project = Solution.GetProject("Calamari.AzureWebApp.NetCoreShim");
                            if (project is null)
                            {
                                Log.Error("Failed to find Calamari.AzureWebApp.NetCoreShim project");
                                return;
                            }

                            var publishPath = PublishDirectory / project.Name;

                            //as this is the only Net 4.6.2 application left, we do a build and restore here
                            DotNetPublish(s => s
                                               .SetConfiguration(Configuration)
                                               .SetProject(project.Path)
                                               .SetFramework("net462")
                                               .SetVersion(NugetVersion.Value)
                                               .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                               .SetOutput(publishPath));

                            Ci.ZipFolderAndUploadArtifact(publishPath, ArtifactsDirectory / "netcoreshim.zip");
                        });
}