using System;
using System.IO;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.PackSashimi);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Internal Nuget Feed URL")]
    readonly string InternalNugetFeedUrl;
    [Parameter("Internal Nuget Feed URL ApiKey")]
    readonly string InternalNugetFeedApiKey;

    [Solution] readonly Solution Solution;
    [GitVersion(Framework = "net5.0")] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PublishDirectory => RootDirectory / "publish";
    AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(PublishDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersion)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .OnlyWhenStatic(() => IsLocalBuild)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetNoBuild(true));
        });

    Target PublishCalamariProjects => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = SourceDirectory.GlobFiles("**/Calamari*.csproj"); //We need Calamari & Calamari.Tests
            foreach(var project in projects)
            {

                var calamariFlavour = XmlTasks.XmlPeekSingle(project, "Project/PropertyGroup/AssemblyName");

                var frameworks = XmlTasks.XmlPeekSingle(project, "Project/PropertyGroup/TargetFrameworks") ??
                                 XmlTasks.XmlPeekSingle(project, "Project/PropertyGroup/TargetFramework");

                foreach(var framework in frameworks.Split(';'))
                {
                    void RunPublish(string runtime, string platform)
                    {
                        DotNetPublish(s => s
                            .SetProject(project)
                            .SetConfiguration(Configuration)
                            .SetFramework(framework)
                            .SetRuntime(runtime)
                            .SetOutput(PublishDirectory / calamariFlavour / platform));
                        File.Copy(RootDirectory / "global.json", PublishDirectory / calamariFlavour / platform / "global.json");
                    }

                    if(framework == "net5.0")
                    {
                        var runtimes = XmlTasks.XmlPeekSingle(project, "Project/PropertyGroup/RuntimeIdentifiers")?.Split(';');
                        foreach (var runtime in runtimes)
                        {
                            RunPublish(runtime, runtime);
                        }
                    }
                    else
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            RunPublish(null, "netfx");
                        }
                        else
                        {
                            Logger.Warn($"Skipping building {framework} - can't build netfx on non Windows OS");
                        }
                    }
                }
                Logger.Trace($"{PublishDirectory}/{calamariFlavour}");

                CompressionTasks.CompressZip(PublishDirectory / calamariFlavour, $"{ArtifactsDirectory / calamariFlavour}.zip");
            }
        });

    Target PublishSashimiTestProjects => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = SourceDirectory.GlobFiles("**/Sashimi.*.Tests.csproj");
            foreach(var project in projects)
            {
                var sashimiFlavour = XmlTasks.XmlPeekSingle(project, "Project/PropertyGroup/AssemblyName");

                DotNetPublish(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutput(PublishDirectory / sashimiFlavour));

                File.Copy(RootDirectory / "global.json", PublishDirectory / sashimiFlavour / "global.json");
                Logger.Trace($"{PublishDirectory}/{sashimiFlavour}");
                CompressionTasks.CompressZip(PublishDirectory / sashimiFlavour, $"{ArtifactsDirectory / sashimiFlavour}.zip");
            }
        });

    Target PackSashimi => _ => _
        .DependsOn(PublishSashimiTestProjects)
        .DependsOn(PublishCalamariProjects)
        .Produces
        (
            ArtifactsDirectory / "Calamari.GoogleCloudScripting.zip",
            ArtifactsDirectory / "Sashimi.GoogleCloud.Accounts.{GitVersion.NuGetVersion}.nupkg",
            ArtifactsDirectory / "Sashimi.GoogleCloud.Scripting.{GitVersion.NuGetVersion}.nupkg",
            ArtifactsDirectory / "Sashimi.GoogleCloud.Accounts.Tests.zip",
            ArtifactsDirectory / "Sashimi.GoogleCloud.Scripting.Tests.zip"
        )
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetNoBuild(false) // Don't change this flag we need it because of https://github.com/dotnet/msbuild/issues/5566
                .SetVersion(GitVersion.NuGetVersion));

            ArtifactsDirectory.GlobFiles("*symbols*").ForEach(DeleteFile);
        });

    // ReSharper disable once UnusedMember.Local - it is actually used (see TriggeredBy)
    Target CopyToLocalPackages => _ => _
        .DependsOn(Test)
        .TriggeredBy(PackSashimi)
        .Unlisted()
        .OnlyWhenStatic(() => IsLocalBuild)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            ArtifactsDirectory
                .GlobFiles($"Sashimi.*.{GitVersion.NuGetVersion}.nupkg")
                .ForEach(sourceFile => CopyFile(sourceFile, LocalPackagesDirectory / Path.GetFileName(sourceFile)));
        });

    [PublicAPI]
    Target Publish => _ => _
        .Requires(() => InternalNugetFeedUrl)
        .DependsOn(PackSashimi)
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
                .SetSource(InternalNugetFeedUrl)
                .SetTargetPath("*.nupkg")
                .SetApiKey(InternalNugetFeedApiKey)
                .SetTimeout(1200)
            );
    });
}
