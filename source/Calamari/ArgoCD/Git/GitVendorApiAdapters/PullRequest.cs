#nullable enable
using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public record PullRequest(string Title, long Number, string Url);
}