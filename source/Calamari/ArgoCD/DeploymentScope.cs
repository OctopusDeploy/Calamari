#nullable enable
using System;
using Calamari.ArgoCD.Models;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD;

public record DeploymentScope(ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant)
{
    public bool Matches(AnnotationScope annotationScope)
    {
        return Project == annotationScope.Project
               && Environment == annotationScope.Environment
               && Tenant == annotationScope.Tenant;
    }
}