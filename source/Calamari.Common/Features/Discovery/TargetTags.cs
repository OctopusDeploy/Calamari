using System;

namespace Calamari.Common.Features.Discovery;

public class TargetTags(
    string? environment,
    string? role,
    string? project,
    string? space,
    string? tenant,
    string? tenantedDeploymentMode)
{
    public const string EnvironmentTagName = "octopus-environment";
    public const string RoleTagName = "octopus-role";
    public const string ProjectTagName = "octopus-project";
    public const string SpaceTagName = "octopus-space";
    public const string TenantTagName = "octopus-tenant";
    public const string TenantedDeploymentModeTagName = "octopus-tenantedDeploymentMode";

    public string? Environment { get; } = environment;
    public string? Role { get; } = role;
    public string? Project { get; } = project;
    public string? Space { get; } = space;
    public string? Tenant { get; } = tenant;
    public string? TenantedDeploymentMode { get; } = tenantedDeploymentMode;
}