using Calamari.Build.Utilities;

namespace Calamari.Build;

public partial class Build
{
    Target PublishNukeBuild =>
        d =>
            d.Executes(() =>
                       {
                           const string runtime = "win-x64";
                           var nukeBuildOutputDirectory = BuildDirectory / "outputs" / runtime / "nukebuild";
                           nukeBuildOutputDirectory.CreateOrCleanDirectory();
                           
                           DotNetPublish(p => p
                                              .SetProject(RootDirectory / "build" / "_build.csproj")
                                              .SetConfiguration(Configuration)
                                              .SetRuntime(runtime)
                                              .EnableSelfContained());
                           
                           Ci.ZipFolderAndUploadArtifact(nukeBuildOutputDirectory, ArtifactsDirectory / $"nukebuild.{runtime}.zip");
                       }); 
}