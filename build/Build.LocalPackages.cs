using System.Text.RegularExpressions;

namespace Calamari.Build;

public partial class Build
{
    
    Target CopyToLocalPackages =>
        d =>
            d.Requires(() => IsLocalBuild)
             .DependsOn(PublishCalamariProjects)
             .Executes(() =>
                       {
                           Directory.CreateDirectory(LocalPackagesDirectory);
                           foreach (AbsolutePath file in Directory.GetFiles(ArtifactsDirectory, "Octopus.Calamari.*.nupkg"))
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
    
    Target BuildLocal => d =>
                             d.Requires(() => IsLocalBuild)
                              .DependsOn(PublishCalamariProjects)
                              .DependsOn(PackCalamariConsolidatedNugetPackage)
                              .DependsOn(UpdateCalamariVersionOnOctopusServer);

    // Sets the Octopus.Server.csproj Calamari.Consolidated package version
    void SetOctopusServerCalamariVersion(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        text = Regex.Replace(text, @"<BundledCalamariVersion>([\S])+</BundledCalamariVersion>",
                             $"<BundledCalamariVersion>{NugetVersion.Value}</BundledCalamariVersion>");
        File.WriteAllText(projectFile, text);
    }
}