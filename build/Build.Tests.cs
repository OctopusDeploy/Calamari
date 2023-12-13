using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Serilog;

namespace Calamari.Build;

partial class Build
{
    [Parameter(Name = "CalamariFlavour")] readonly string CalamariFlavourToTest;
    [Parameter(Name = "VSTest_TestCaseFilter")] readonly string CalamariFlavourTestCaseFilter;
    [Parameter(Name = "DefaultGitBranch")] readonly string MainBranchName;
    
    [PublicAPI]
    Target DetermineAffectedTests =>
        target => target
            .Produces(RootDirectory / "affected.proj")
            .Executes(async () =>
            {
                if (GitVersionInfo.BranchName == MainBranchName)
                {
                    Log.Information("On default branch, nothing to calculate");
                    return;
                }

                GitTasks.Git($"fetch origin {MainBranchName}");
                var mainCommitSha = GitTasks.Git($"show-ref {MainBranchName} -s").FirstOrDefault().Text;
                var mergeBase = GitTasks.Git($"merge-base {GitVersionInfo.Sha} {mainCommitSha}").FirstOrDefault();

                if (string.IsNullOrWhiteSpace(mergeBase.Text))
                {
                    Log.Warning("No common merge base found. Not publishing an affected.proj artifact");
                    return;
                }

                DotNetTasks.DotNetToolRestore();
                DotNetTasks.DotNet($"affected --from {GitVersionInfo.Sha} --to {mergeBase.Text} --verbose");

                if (File.Exists("affected.proj"))
                {
                    Log.Information("Published affected.proj artifact");
                }
                else
                {
                    Log.Warning("Did not publish affected.proj artifact");
                }
            });

    [PublicAPI]
    Target TestCalamariFlavourProject =>
        target => target
            .Executes(async () =>
                      {
                          var testProject = $"Calamari.{CalamariFlavourToTest}.Tests";

                          var affectedProjectFile = RootDirectory / "affected.proj";
                          bool isAffected;
                          if (affectedProjectFile.FileExists())
                          {
                              Log.Information("Affected projects analysis found; checking to see if {TestProject} is affected", testProject);
                              var contents = await File.ReadAllTextAsync(affectedProjectFile);

                              isAffected = contents.Contains(testProject);
                          }
                          else
                          {
                              Log.Information("Affected projects analysis not found; assuming {TestProject} *is* affected", testProject);
                              isAffected = true;
                          }

                          if (isAffected)
                          {
                              Log.Verbose("{TestProject} tests will be executed", testProject);

                              DotNetTasks.DotNetTest(settings => settings
                                                                 .SetProjectFile($"{testProject}.dll")
                                                                 .SetFilter(CalamariFlavourTestCaseFilter)
                                                                 .SetLoggers("trx"));
                          }
                          else
                          {
                              Log.Information("{TestProject} is not affected, so no tests will be executed", testProject);
                              Log.Information($"##teamcity[testStarted name='{testProject}-NoTests' captureStandardOutput='false']");
                              Log.Information($"##teamcity[testFinished name='{testProject}-NoTests' duration='0']");
                          }
                      });
}

