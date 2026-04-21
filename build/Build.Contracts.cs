namespace Calamari.Build;

public partial class Build
{
    Target PackContractsProject =>
        d =>
            d.DependsOn(RestoreSolution)
             .Executes(() =>
                       {
                           var project = Solution.GetProject("Calamari.Contracts")!;

                           Log.Information("Packaging {ProjectName}", project.Name);

                           var buildDirectory = KnownPaths.SourceDirectory / project.Name / "bin" / Configuration;

                           //Build the consolidated package libraries
                           DotNetBuild(s =>
                                           s.SetConfiguration(Configuration)
                                            .SetProjectFile(project));

                           File.Copy(KnownPaths.RootDirectory / "global.json", buildDirectory / "global.json");

                           //sign the built directory
                           SignDirectory(buildDirectory);

                           //pack the project
                           DotNetPack(s => s
                                           .SetConfiguration(Configuration)
                                           .SetOutputDirectory(KnownPaths.ArtifactsDirectory)
                                           .SetProject(project)
                                           .EnableNoBuild()
                                           .EnableNoRestore()
                                           .EnableIncludeSource()
                                           .SetVersion(NugetVersion.Value));
                       });
}