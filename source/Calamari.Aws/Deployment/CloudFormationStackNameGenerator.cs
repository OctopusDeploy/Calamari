using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Deployment
{
    public static class CloudFormationStackNameGenerator
    {
        public static string GetStackName(IVariables variables, params string[] extras)
        {
            // Apply camelCase to each non-null extra
            var sanitisedExtras = extras
                                  .Where(e => !string.IsNullOrWhiteSpace(e))
                                  .Select(e => e.ToCamelCase()) // use ! because we've filtered null/empty
                                  .ToList();

            // Get and sanitize environment ID
            var environmentId = variables.Get(DeploymentEnvironment.Id);
            var sanitisedEnvironmentId = !string.IsNullOrWhiteSpace(environmentId)
                ? environmentId.ToCamelCase()
                : string.Empty;

            // Get and sanitize tenant ID
            var tenantId = variables.Get(DeploymentVariables.Tenant.Id);
            var sanitisedTenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId.ToCamelCase()
                : "untenanted";

            // Combine all parts
            var parts = new List<string> { "cf" };
            parts.AddRange(sanitisedExtras);
            parts.Add(sanitisedEnvironmentId);
            parts.Add(sanitisedTenantId);

            var stackName = string.Join("-", parts
                                             .Select(p => p.Trim())
                                             .Where(p => !string.IsNullOrEmpty(p)));

            return stackName.Length > 128 ? stackName.Substring(0, 128) : stackName;
        }
    }
}