#nullable enable
using System;
using Calamari.ArgoCD.Models;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD;

public record DeploymentScope(ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant, ActionSlug Action)
{
    public bool Matches(AnnotationScope annotationScope)
    {
        return Project == annotationScope.Project
               && Environment == annotationScope.Environment
               && Tenant == annotationScope.Tenant
               && MatchesAction(annotationScope);
    }

    // An unannotated source means any action can act on it
    bool MatchesAction(AnnotationScope annotationScope)
        => annotationScope.Action is null || Action == annotationScope.Action;
}
