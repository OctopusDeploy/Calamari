#nullable enable
using System;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Dtos;

public record DeploymentScope(ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant)
{
    public bool Matches(AnnotationScope annotationScope)
    {
        return Project.Equals(annotationScope.Project)
               && Environment.Equals(annotationScope.Environment)
               && ((Tenant == null && annotationScope.Tenant == null) || (Tenant != null && Tenant.Equals(annotationScope.Tenant)));
    }
}