// ReSharper disable RedundantUsingDirective
using System;

public static class Git
{
    public static string? DeriveGitBranch() => Environment.GetEnvironmentVariable("OCTOVERSION_CurrentBranch");
}