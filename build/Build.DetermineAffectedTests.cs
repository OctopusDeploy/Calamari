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
    [Parameter(Name = "DefaultGitBranch")] readonly string MainBranchName;
    
    [PublicAPI]
    Target DetermineAffectedTests =>
        target => target
            .Executes(() =>
            {
                if (GitVersionInfo is null)
                    throw new ArgumentNullException(nameof(GitVersionInfo));
                
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
                    TeamCity.Instance.PublishArtifacts("affected.proj");
                    Log.Information("Published affected.proj artifact");
                }
                else
                {
                    Log.Warning("Did not publish affected.proj artifact");
                }
            });
}

