using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Calamari.Build;

public partial class Build
{
    static AbsolutePath LocalPackagesDirectory => KnownPaths.RootDirectory / ".." / "LocalPackages";
    
    Target CopyToLocalPackages =>
        d =>
            d.Requires(() => IsLocalBuild)
             .DependsOn(PublishCalamariProjects)
             .DependsOn(PackCalamariConsolidatedNugetPackage)
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
                           var serverProjectFile = KnownPaths.RootDirectory / ".." / "OctopusDeploy" / "source" / "Octopus.Server" / "Octopus.Server.csproj";
                           var serverNugetConfigFile = KnownPaths.RootDirectory / ".." / "OctopusDeploy" / "NuGet.Config";
                           var projectFileExists = File.Exists(serverProjectFile);
                           var nugetFileExists = File.Exists(serverNugetConfigFile);
                           if (projectFileExists && nugetFileExists)
                           {
                               Log.Information("Setting Calamari version in Octopus Server "
                                               + "project {ServerProjectFile} to {NugetVersion}",
                                               serverProjectFile, NugetVersion.Value);
                               SetOctopusServerCalamariVersion(serverProjectFile);
                               AddLocalPackagesSource(serverNugetConfigFile);
                           }
                           else
                           {
                               if (!projectFileExists)
                               {
                                   Log.Warning("Could not set Calamari version in Octopus Server project "
                                               + "{ServerProjectFile} to {NugetVersion} as could not find "
                                               + "project file",
                                               serverProjectFile, NugetVersion.Value);
                               }
                               else if (!nugetFileExists)
                               {
                                   Log.Warning("Could not set Calamari version in Octopus Server project "
                                               + "{ServerProjectFile} to {NugetVersion} as could not find "
                                               + "nuget config file",
                                               serverNugetConfigFile, NugetVersion.Value);
                               }

                           }
                       });
    void SetOctopusServerCalamariVersion(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        text = Regex.Replace(text, @"<BundledCalamariVersion>([\S])+</BundledCalamariVersion>",
                             $"<BundledCalamariVersion>{NugetVersion.Value}</BundledCalamariVersion> <!--DO NOT COMMIT-->");
        File.WriteAllText(projectFile, text);
    }

    void AddLocalPackagesSource(string nugetConfigFile)
    {
        var doc = XDocument.Load(nugetConfigFile);
        
        // Add LocalPackages to packageSources
        var packageSources = doc.Descendants("packageSources").FirstOrDefault();
        if (packageSources == null)
            throw new InvalidOperationException("Could not find <packageSources> element in NuGet.config");
    
        packageSources.Add(new XElement("add",
                                        new XAttribute("key", "LocalPackages"),
                                        new XAttribute("value", "../LocalPackages")));
    
        packageSources.Add(new XComment("DO NOT COMMIT"));
    
        // Add LocalPackages to packageSourceMapping
        var packageSourceMapping = doc.Descendants("packageSourceMapping").FirstOrDefault();
        if (packageSourceMapping == null)
            throw new InvalidOperationException("Could not find <packageSourceMapping> element in NuGet.config");
    
        var clearElement = packageSourceMapping.Element("clear");
        if (clearElement == null)
            throw new InvalidOperationException("Could not find <clear /> element in <packageSourceMapping>");
    
        var localPackagesMapping = new XElement("packageSource",
                                                new XAttribute("key", "LocalPackages"),
                                                new[] { 
                                                    "Octopus.Calamari.Consolidated", 
                                                    "Octopus.Calamari.ConsolidatedPackage", 
                                                    "Octopus.Calamari.ConsolidatedPackage.Api" 
                                                }.Select(p => new XElement("package", new XAttribute("pattern", p))));
    
        clearElement.AddAfterSelf(localPackagesMapping);
    
        doc.Save(nugetConfigFile);
    }
}