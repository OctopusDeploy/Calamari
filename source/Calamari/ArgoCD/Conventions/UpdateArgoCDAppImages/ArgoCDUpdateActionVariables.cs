using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;

// Everytime we Update something in Argo we will need these variables to perform the task - so centralising them for now.
public class ArgoCDUpdateActionVariables : IArgoCDActionVariables
{
    readonly ArgoCDActionVariablesBase wrappedVariablesBase;
    public string ProjectSlug => wrappedVariablesBase.ProjectSlug;
    public string EnvironmentSlug => wrappedVariablesBase.EnvironmentSlug;
    public string? TenantSlug => wrappedVariablesBase.TenantSlug;
    public List<ContainerImageReference> ImageReferences { get; }
    public GitCommitSummary CommitMessageSummary => wrappedVariablesBase.CommitMessageSummary;
    public string? CommitMessageDescription => wrappedVariablesBase.CommitMessageDescription;
    
    public bool CreatePullRequest => wrappedVariablesBase.CreatePullRequest;

    internal ArgoCDUpdateActionVariables(
        ArgoCDActionVariablesBase wrappedVariablesBase,
        List<ContainerImageReference> imageReferences)
    {
        this.wrappedVariablesBase = wrappedVariablesBase;
        ImageReferences = imageReferences;
    }
}
