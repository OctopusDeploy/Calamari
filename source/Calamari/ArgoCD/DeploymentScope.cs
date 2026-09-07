#nullable enable
using System;
using Calamari.ArgoCD.Models;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD;

public record DeploymentScope(ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant, StepSlug Step)
{
    public bool Matches(AnnotationScope annotationScope)
    {
        return Project == annotationScope.Project
               && Environment == annotationScope.Environment
               && Tenant == annotationScope.Tenant
               && MatchesStep(annotationScope);
    }

    // An unannotated source means any step can act on it
    bool MatchesStep(AnnotationScope annotationScope)
        => annotationScope.Step is null || Step == annotationScope.Step;
}
