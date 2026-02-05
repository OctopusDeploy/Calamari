#nullable enable
using System;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Dtos;

public record AnnotationScope(ProjectSlug? Project, EnvironmentSlug? Environment, TenantSlug? Tenant);