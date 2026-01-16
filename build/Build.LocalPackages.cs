using System.Text.RegularExpressions;

namespace Calamari.Build;

public partial class Build
{
    static AbsolutePath LocalPackagesDirectory => KnownPaths.RootDirectory / ".." / "LocalPackages";
    
    Target CopyToLocalPackages =>
        d =>
            d.Requires(() => IsLocalBuild)
             .DependsOn(PublishCalamariProjects)
             .Executes(() =>
                       {
                           Directory.CreateDirectory(LocalPackagesDirectory);
                           foreach (AbsolutePath file in Directory.GetFiles(KnownPaths.ArtifactsDirectory, "Octopus.Calamari.*.nupkg"))
                           {
                               var target = LocalPackagesDirectory / Path.GetFileName(file);
                               file.Copy(target, ExistsPolicy.FileOverwrite);
                           }
                       });

    Target UpdateCalamariVersionOnOctopusServer =>
        d =>
            d.OnlyWhenStatic(() => SetOctopusServerVersion)
             .Requires(() => IsLocalBuild)
             .DependsOn(CopyToLocalPackages)
             .Executes(() =>
                       {
                           var serverProjectFile = RootDirectory / ".." / "OctopusDeploy" / "source" / "Octopus.Server" / "Octopus.Server.csproj";
                           if (File.Exists(serverProjectFile))
                           {
                               Log.Information("Setting Calamari version in Octopus Server "
                                               + "project {ServerProjectFile} to {NugetVersion}",
                                               serverProjectFile, NugetVersion.Value);
                               SetOctopusServerCalamariVersion(serverProjectFile);
                           }
                           else
                           {
                               Log.Warning("Could not set Calamari version in Octopus Server project "
                                           + "{ServerProjectFile} to {NugetVersion} as could not find "
                                           + "project file",
                                           serverProjectFile, NugetVersion.Value);
                           }
                       });
    void SetOctopusServerCalamariVersion(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        text = Regex.Replace(text, @"<BundledCalamariVersion>([\S])+</BundledCalamariVersion>",
                             $"<BundledCalamariVersion>{NugetVersion.Value}</BundledCalamariVersion>");
        File.WriteAllText(projectFile, text);
    }
}