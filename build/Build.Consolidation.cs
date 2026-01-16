using System.Collections.Generic;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NuGet.Packaging;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NuGet;
using Octopus.Calamari.ConsolidatedPackage;

namespace Calamari.Build;

public partial class Build
{    
    static readonly List<string> NuGetPackagesToExcludeFromConsolidation =
    [
        "Octopus.Calamari.CloudAccounts",
        "Octopus.Calamari.Common", 
        "Octopus.Calamari.ConsolidateCalamariPackages",
        "Octopus.Calamari.ConsolidatedPackage",
        "Octopus.Calamari.ConsolidatedPackage.Api"
    ];
    
    static AbsolutePath ConsolidateCalamariPackagesProject => KnownPaths.SourceDirectory / "Calamari.ConsolidateCalamariPackages.Tests" / "Calamari.ConsolidateCalamariPackages.Tests.csproj";
    static AbsolutePath ConsolidatedPackageDirectory => KnownPaths.ArtifactsDirectory / "consolidated";

    Target PackageConsolidatedCalamariZip =>
        d =>
            d.Executes(() =>
                       {
                           var artifacts = Directory.GetFiles(KnownPaths.ArtifactsDirectory, "*.nupkg")
                                                    .Where(a => !NuGetPackagesToExcludeFromConsolidation.Any(a.Contains));

                           var packageReferences = new List<BuildPackageReference>();
                           foreach (var artifact in artifacts)
                           {
                               using var zip = ZipFile.OpenRead(artifact);
                               var nuspecFileStream = zip.Entries.First(e => e.Name.EndsWith(".nuspec")).Open();
                               var nuspecReader = new NuspecReader(nuspecFileStream);
                               var metadata = nuspecReader.GetMetadata().ToList();
                               packageReferences.Add(new BuildPackageReference
                               {
                                   Name = Regex.Replace(metadata.Where(kvp => kvp.Key == "id").Select(i => i.Value).First(), @"^Octopus\.", ""),
                                   Version = metadata.Where(kvp => kvp.Key == "version").Select(i => i.Value).First(),
                                   PackagePath = artifact
                               });
                           }

                           foreach (var flavour in GetCalamariFlavours())
                           {
                               if (Solution.GetProject(flavour) != null)
                               {
                                   packageReferences.Add(new BuildPackageReference
                                   {
                                       Name = flavour,
                                       Version = NugetVersion.Value,
                                       PackagePath = KnownPaths.ArtifactsDirectory / $"{flavour}.zip"
                                   });
                               }
                           }

                           Directory.CreateDirectory(ConsolidatedPackageDirectory);
                           var (result, packageFilename) = new Consolidate(Log.Logger).Execute(ConsolidatedPackageDirectory, packageReferences);

                           if (!result)
                               throw new Exception("Failed to consolidate calamari Packages");

                           ConsolidatedPackagePath = packageFilename;
                           Log.Information("Created consolidated package zip: {PackageFilename}", packageFilename);
                       });

    Target CalamariConsolidationVerification =>
        d =>
            d.DependsOn(PackageConsolidatedCalamariZip)
             .OnlyWhenDynamic(() => string.IsNullOrEmpty(TargetRuntime), "TargetRuntime is not restricted")
             .Executes(() =>
                       {
                           Environment.SetEnvironmentVariable("CONSOLIDATED_ZIP", ConsolidatedPackagePath);
                           Environment.SetEnvironmentVariable("EXPECTED_VERSION", NugetVersion.Value);
                           Environment.SetEnvironmentVariable("IS_WINDOWS", OperatingSystem.IsWindows().ToString());

                           DotNetTest(s => s
                                           .SetProjectFile(ConsolidateCalamariPackagesProject)
                                           .SetConfiguration(Configuration)
                                           .SetProcessArgumentConfigurator(args =>
                                                                               args.Add("--logger:\"console;verbosity=detailed\"")
                                                                                   .Add("--")
                                                                                   .Add("NUnit.ShowInternalProperties=true")));
                       });

    Target PackConsolidationLibrariesNugetPackages =>
        d =>
            d.DependsOn(CalamariConsolidationVerification)
             .Executes(() =>
                       {
                           // Pack the Consolidation Libraries
                           const string consolidateCalamariPackagesProjectPrefix = "Calamari.ConsolidateCalamariPackages";
                           var consolidationLibraryProjects = Solution.Projects.Where(project => project.Name.StartsWith(consolidateCalamariPackagesProjectPrefix));

                           foreach (var project in consolidationLibraryProjects)
                           {
                               Log.Information("Packaging {ProjectName}", project.Name);
                               
                               var buildDirectory = KnownPaths.SourceDirectory / project.Name / "bin" / Configuration;

                               //Build the consolidated package libraries
                               DotNetBuild(s =>
                                                 s.SetConfiguration(Configuration)
                                                  .SetProjectFile(project));

                               File.Copy(RootDirectory / "global.json", buildDirectory / "global.json");

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
                           }
                       });

    Target PackCalamariConsolidatedNugetPackage =>
        d =>
            d.DependsOn(PackConsolidationLibrariesNugetPackages)
             .Executes(() =>
                       {
                           NuGetPack(s => s.SetTargetPath(KnownPaths.BuildDirectory / "Calamari.Consolidated.nuspec")
                                           .SetBasePath(KnownPaths.BuildDirectory)
                                           .SetVersion(NugetVersion.Value)
                                           .SetOutputDirectory(KnownPaths.ArtifactsDirectory));
                       });
}