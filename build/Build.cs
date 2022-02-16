using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;

class Build : NukeBuild
{
    const string LinuxRuntime = "linux-x64";
    const string WindowsRuntime = "win-x64";
    const string CiBranchNameEnvVariable = "CALAMARI_CurrentBranch";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] 
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Test filter expression", Name = "where")] 
    readonly string TestFilter = string.Empty;

    [Solution] 
    readonly Solution? Solution;
    
    [Parameter("Whether to auto-detect the branch name - this is okay for a local build, but should not be used under CI.")] 
    readonly bool AutoDetectBranch = IsLocalBuild;
    
    [Parameter("Branch name for Calamari to use to calculate the version number. Can be set via the environment variable " + CiBranchNameEnvVariable + ".", Name = CiBranchNameEnvVariable)]
    string? BranchName { get; set; }
    
    readonly string OctoVersionInfoNullMessage = 
        $"{nameof(OctoVersionInfo)} is null - this should be set by the Nuke {nameof(OctoVersionAttribute)}." + 
        $" Ensure the CI server has set the \"{CiBranchNameEnvVariable}\" environment variable.";
    
    readonly string SolutionNullMessage = 
        $"{nameof(Solution)} is null - this should be set by the Nuke {nameof(SolutionAttribute)}.";

    [OctoVersion(BranchParameter = nameof(BranchName), AutoDetectBranchParameter = nameof(AutoDetectBranch))] 
    public OctoVersionInfo? OctoVersionInfo;
    
    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";
    
    public static int Main() => Execute<Build>(x => x.BuildLocal);
    
    Target CheckForbiddenWords => _ => _.Executes(() =>
                                                  {
                                                      Console.WriteLine("Checking codebase for forbidden words.");

                                                      var outputs = Git($"grep -i -I -n -f ForbiddenWords.txt -- \"./*\" \":(exclude)ForbiddenWords.txt\"", RootDirectory);

                                                      var filesContainingForbiddenWords = outputs.ToArray();
                                                      if (filesContainingForbiddenWords.Any())
                                                          throw new Exception("Found forbidden words in the following files, "
                                                                              + "please clean them up:\r\n"
                                                                              + string.Join("\r\n", filesContainingForbiddenWords.Select(o => o.Text)));

                                                      Console.WriteLine("Sanity check passed.");
                                                  });

    Target Clean => _ => _.Executes(() =>
                                    {
                                        SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
                                        EnsureCleanDirectory(ArtifactsDirectory);
                                    });

    Target Restore => _ => _.DependsOn(Clean)
                            .Executes(() =>
                                      {
                                          var localRuntime = WindowsRuntime;

                                          if (!OperatingSystem.IsWindows())
                                          {
                                              localRuntime = LinuxRuntime;
                                          }

                                          DotNetRestore(
                                             s => s.SetProjectFile(Solution)
                                                   .SetRuntime(localRuntime)
                                                   .SetProperty("DisableImplicitNuGetFallbackFolder", true));
                                      });

    Target Compile => _ => _.DependsOn(CheckForbiddenWords)
                            .DependsOn(Restore)
                            .Executes(async () =>
                                      {
                                          if (OctoVersionInfo is null)
                                          {
                                              throw new NullReferenceException(OctoVersionInfoNullMessage);
                                          }

                                          if (Solution is null)
                                          {
                                              throw new NullReferenceException(SolutionNullMessage);
                                          }

                                          DotNetBuild(s => s.SetProjectFile(Solution)
                                                            .SetConfiguration(Configuration)
                                                            .EnableNoRestore()
                                                            .SetVersion(OctoVersionInfo.FullSemVer)
                                                            .SetInformationalVersion(OctoVersionInfo.InformationalVersion));
                                      });
    Target PackBinaries => _ => _.DependsOn(Compile)
                                 .Executes(async () =>
                                           {
                                               Console.WriteLine("PackBinaries");
                                               //TODO:
                                           });

    Target PackTests => _ => _.DependsOn(Compile)
                              .Executes(async () =>
                                        {
                                            Console.WriteLine("PackTests");
                                            //TODO:
                                        });
    Target Pack => _ => _.DependsOn(PackBinaries)
                         .DependsOn(PackTests);

    Target CopyToLocalPackages => _ => _
                                      //TODO: What's the Nuke equiv of .WithCriteria(BuildSystem.IsLocalBuild)
                                      .Executes(() =>
                                                {
                                                    Console.WriteLine("CopyToLocalPackages");
                                                    //TODO:
                                                });

    Target SetOctopusServerVersion => _ => _
                                          //TODO:     .WithCriteria(BuildSystem.IsLocalBuild)
                                          //TODO:     .WithCriteria(setOctopusServerVersion)
                                          .Executes(() =>
                                                    {
                                                        Console.WriteLine("SetOctopusServerVersion");
                                                        //TODO:
                                                    });

    Target SetTeamCityVersion => _ => _.Executes(() =>
                                                 {
                                                     Console.WriteLine("SetTeamCityVersion");
                                                     //TODO:
                                                 });

    Target BuildLocal => _ => _.DependsOn(PackBinaries)
                               .DependsOn(CopyToLocalPackages)
                               .DependsOn(SetOctopusServerVersion);


    Target BuildCI => _ => _.DependsOn(SetTeamCityVersion)
                            .DependsOn(Pack)
                            .DependsOn(CopyToLocalPackages)
                            .DependsOn(SetOctopusServerVersion);
}