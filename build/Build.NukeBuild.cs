using Calamari.Build.Utilities;

namespace Calamari.Build;

public partial class Build
{
    Target PublishNukeBuild =>
        d =>
            d.Executes(() =>
                       {
                           //we aren't currently testing on Mac, so skip that
                           string[] runtimeIdentifiers = ["win-x64","linux-x64","linux-arm","linux-arm64"];
                           foreach (var runtime in runtimeIdentifiers)
                           {
                               var nukeBuildOutputDirectory = KnownPaths.BuildDirectory / "outputs" / runtime / "nukebuild";
                               nukeBuildOutputDirectory.CreateOrCleanDirectory();

                               DotNetPublish(p => p
                                                  .SetProject(KnownPaths.RootDirectory / "build" / "_build.csproj")
                                                  .SetConfiguration(Configuration)
                                                  .SetRuntime(runtime)
                                                  .SetVersion(NugetVersion.Value)
                                                  .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                                  .SetVerbosity(BuildVerbosity)
                                                  .EnableSelfContained());

                               Ci.ZipFolderAndUploadArtifact(nukeBuildOutputDirectory, KnownPaths.ArtifactsDirectory / $"nukebuild.{runtime}.zip");
                           }
                       });
}