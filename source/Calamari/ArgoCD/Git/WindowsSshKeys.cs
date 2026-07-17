using System;
using Calamari.Common.Plumbing;
using NuGet.Commands;

namespace Calamari.ArgoCD.Git;

// Our fork LibGit2Sharp uses WinCNG for it's SSH support, and WinCNG does not support some keys.
// This first implementation blocks on all as we want to get the feature out for Linux first and
// we'll come back to attempt to handle it more gracefully at a later date.
public class WindowsSshKeys
{
    public static void AssertSupported(IGitConnection? connection)
    {
        if (!CalamariEnvironment.IsRunningOnWindows) return;
        if (connection is not SshKeyGitConnection) return;

        throw new CommandException(
            "SSH credentials are not currently supported for Git operations running on Windows. Use HTTPS credentials (username + password or PAT) instead or run the deployment on a Linux worker.");
    }
}