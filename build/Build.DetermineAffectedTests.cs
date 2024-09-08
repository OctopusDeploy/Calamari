using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Serilog;

namespace Calamari.Build;

partial class Build
{

    [Parameter(Name = "BUILD_VCS_NUMBER")]
    readonly string CommitSha;

    [PublicAPI]
    Target DetermineAffectedTests =>
        target => target
            .Executes(() =>
            {
                File.WriteAllText("placeholder.txt",
                    "TeamCity can't stand to have no artifacts in a consuming configuration, so this is a placeholder");
                TeamCity.Instance.PublishArtifacts("placeholder.txt");

                if (BranchName == DefaultBranchName)
                {
                    Log.Information("On default branch, nothing to calculate");
                    return;
                }
 
                GitTasks.Git($"fetch origin {DefaultBranchName}");
                var mainCommitSha = GitTasks.Git($"show-ref {DefaultBranchName} -s").FirstOrDefault().Text;
                var mergeBase = GitTasks.Git($"merge-base {CommitSha} {mainCommitSha}").FirstOrDefault();

                if (string.IsNullOrWhiteSpace(mergeBase.Text))
                {
                    Log.Warning("No common merge base found. Not publishing an affected.proj artifact");
                    return;
                }

                DotNetTasks.DotNetToolRestore();
                DotNetTasks.DotNet($"affected --from {CommitSha} --to {mergeBase.Text} --verbose");

                if (File.Exists("affected.proj"))
                {
                    TeamCity.Instance.PublishArtifacts("affected.proj");
                    Log.Information("Published affected.proj artifact");
                }
                else
                {
                    Log.Warning("Did not publish affected.proj artifact");
                }
            });
}