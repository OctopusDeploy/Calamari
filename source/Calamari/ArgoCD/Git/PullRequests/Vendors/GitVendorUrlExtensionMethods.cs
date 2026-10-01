using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors;

public static class GitVendorUrlExtensionMethods
{
    const string GitExtension = ".git";
    public static string[] SplitPathIntoSegments(this Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        path = StripGitSuffix(path);
        return path.Split('/');
    }

    static string StripGitSuffix(string value)
    {
        return value.EndsWith(GitExtension, StringComparison.OrdinalIgnoreCase)
            ? value[..^GitExtension.Length]
            : value;
    }
}
