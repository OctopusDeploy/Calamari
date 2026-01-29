using Calamari.Build.Utilities;

namespace Calamari.Build;

public partial class Build
{
    Target PublishNukeBuild =>
        d =>
            d.Executes(() =>
                       {
                           const string runtime = "win-x64";
                           var nukeBuildOutputDirectory = KnownPaths.BuildDirectory / "outputs" / runtime / "nukebuild";
                           nukeBuildOutputDirectory.CreateOrCleanDirectory();

                           DotNetPublish(p => p
                                              .SetProject(KnownPaths.RootDirectory / "build" / "_build.csproj")
                                              .SetConfiguration(Configuration)
                                              .SetRuntime(runtime)
                                              .EnableSelfContained());

                           Ci.ZipFolderAndUploadArtifact(nukeBuildOutputDirectory, KnownPaths.ArtifactsDirectory / $"nukebuild.{runtime}.zip");
                       });
}