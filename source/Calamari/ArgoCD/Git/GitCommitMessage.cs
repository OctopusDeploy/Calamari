#nullable enable
using System;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;

namespace Calamari.ArgoCD.Git
{
    public class GitCommitMessage
    {
        public GitCommitMessage(string summary, string? body = null) : this(new GitCommitSummary(summary), body)
        {
        }

        public GitCommitMessage(GitCommitSummary summary, string? body = null)
        {
            Summary = summary;
            Body = body;
        }

        public GitCommitSummary Summary { get; }
        public string? Body { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Body) ? $"{Summary}" : $"{Summary}\n\n{Body}";
        }
    }
}