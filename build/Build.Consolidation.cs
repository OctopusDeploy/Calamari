using System.Collections.Generic;
using JetBrains.Annotations;
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

    Target PackageConsolidatedCalamariZip =>
        d =>
            d.Executes(() =>
                       {
                           //Look for all zip files in the artifacts directory that aren't tests
                           var artifacts = Directory.GetFiles(KnownPaths.ArtifactsDirectory, "*.zip")
                                                    .Where(a => !NuGetPackagesToExcludeFromConsolidation.Any(a.Contains))
                                                    .Where(a => a.Contains("Tests"));

                           var packageReferences = artifacts.Select(artifactPath => (artifactPath, projectName: Path.GetFileNameWithoutExtension(artifactPath)))
                                                            .Where(x => Solution.GetProject(x.projectName) is not null)
                                                            .Select((x) =>
                                                                    {
                                                                        var (artifactPath, projectName) = x;
                                                                        return new BuildPackageReference
                                                                        {
                                                                            Name = projectName,
                                                                            Version = NugetVersion.Value,
                                                                            PackagePath = artifactPath
                                                                        };
                                                                    })
                                                            .ToList();

                           var consolidatedPackageDirectory = KnownPaths.ArtifactsDirectory / "consolidated";
                           consolidatedPackageDirectory.CreateOrCleanDirectory();
                           
                           var (result, packageFilename) = new Consolidate(Log.Logger).Execute(consolidatedPackageDirectory, packageReferences);

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
                           Environment.SetEnvironmentVariable("IS_WINDOWS", (OperatingSystem.IsWindows() || !IsLocalBuild).ToString());

                           DotNetTest(s => s
                                           .SetProjectFile(ConsolidateCalamariPackagesProject)
                                           .SetConfiguration(Configuration)
                                           .SetProcessArgumentConfigurator(args =>
                                                                               args.Add("--logger:\"console;verbosity=detailed\"")
                                                                                   .Add("--")
                                                                                   .Add("NUnit.ShowInternalProperties=true")));
                       });

    [PublicAPI]
    Target PackCalamariConsolidatedNugetPackage =>
        d =>
            d.DependsOn(CalamariConsolidationVerification)
             .Executes(() =>
                       {
                           NuGetPack(s =>
                                         s.SetTargetPath(BuildDirectory / "Calamari.Consolidated.nuspec")
                                          .SetBasePath(BuildDirectory)
                                          .SetVersion(NugetVersion.Value)
                                          .SetOutputDirectory(ArtifactsDirectory));
                       });
}