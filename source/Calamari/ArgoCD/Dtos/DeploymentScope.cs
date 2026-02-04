#nullable enable
using System;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Dtos;

public record DeploymentScope(ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant)
{
    public bool Matches(AnnotationScope annotationScope)
    {
        return Project == annotationScope.Project
               && Environment == annotationScope.Environment
               && Tenant == annotationScope.Tenant;
    }
}