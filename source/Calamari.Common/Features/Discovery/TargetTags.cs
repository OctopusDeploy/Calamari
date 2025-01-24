using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Common.Features.Discovery
{
    public class TargetTags
    {
        public const string EnvironmentTagName = "octopus-environment";
        public const string RoleTagName = "octopus-role";
        public const string ProjectTagName = "octopus-project";
        public const string SpaceTagName = "octopus-space";
        public const string TenantTagName = "octopus-tenant";
        public const string TenantedDeploymentModeTagName = "octopus-tenantedDeploymentMode";

        public TargetTags(
            string? environment,
            string? role,
            string? project,
            string? space,
            string? tenant,
            string? tenantedDeploymentMode)
        {
            this.Environment = environment;
            this.Role = role;
            this.Project = project;
            this.Space = space;
            this.Tenant = tenant;
            this.TenantedDeploymentMode = tenantedDeploymentMode;
        }

        public string? Environment { get; }
        public string? Role { get; }
        public string? Project { get; }
        public string? Space { get; }
        public string? Tenant { get; }
        public string? TenantedDeploymentMode { get; }
    }
}
