using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Common.Features.Discovery
{
    public class TargetMatchResult
    {
        private readonly string? role;
        private readonly string? tenantedDeploymentMode;
        private readonly IEnumerable<string> failureReasons;

        private TargetMatchResult(string role, string? tenantedDeploymentMode = null)
        {
            this.role = role;
            this.tenantedDeploymentMode = tenantedDeploymentMode;
            this.failureReasons = Enumerable.Empty<string>();
        }

        private TargetMatchResult(IEnumerable<string> failureReasons)
        {
            this.failureReasons = failureReasons;
        }

        public string Role => this.role ?? throw new InvalidOperationException("Cannot get Role from failed target match result.");
        public string? TenantedDeploymentMode => this.tenantedDeploymentMode;
        
        public IEnumerable<string> FailureReasons => this.failureReasons;

        public bool IsSuccess => this.role != null;

        public static TargetMatchResult Success(string role, string? tenantedDeploymentMode) => new TargetMatchResult(role, tenantedDeploymentMode);

        public static TargetMatchResult Failure(IEnumerable<string> failureReasons) => new TargetMatchResult(failureReasons);
    }
}
