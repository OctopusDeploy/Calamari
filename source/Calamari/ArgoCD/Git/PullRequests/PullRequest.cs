#nullable enable
using System;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public record PullRequest(string Title, long Number, string Url);
}