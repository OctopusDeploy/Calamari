#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public interface IArgoCDActionVariables
    {
        public string ProjectSlug { get; }
        public string EnvironmentSlug { get; }
        public string? TenantSlug { get; }
        
        public GitCommitSummary CommitMessageSummary { get; }
        public string? CommitMessageDescription { get; }
    
        public bool CreatePullRequest { get; }
    }

    public class ArgoCDActionVariablesBase : IArgoCDActionVariables
    {
        public ArgoCDActionVariablesBase(string projectSlug,
                                         string environmentSlug,
                                         string? tenantSlug,
                                         GitCommitSummary commitMessageSummary,
                                         string? commitMessageDescription,
                                         bool createPullRequest)
        {
            ProjectSlug = projectSlug;
            EnvironmentSlug = environmentSlug;
            TenantSlug = tenantSlug;
            CommitMessageSummary = commitMessageSummary;
            CommitMessageDescription = commitMessageDescription;
            CreatePullRequest = createPullRequest;
        }

        public string ProjectSlug { get; }
        public string EnvironmentSlug { get; }
        public string? TenantSlug { get; }
        public GitCommitSummary CommitMessageSummary { get; }
        public string? CommitMessageDescription { get; init; }
        public bool CreatePullRequest { get; }
    }
}
