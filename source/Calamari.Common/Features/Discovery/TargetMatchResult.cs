using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Common.Features.Discovery
{
    public class TargetMatchResult
    {
        private readonly string? role;
        private readonly string? reason;

        private TargetMatchResult(string? role, string? reason)
        {
            this.role = role;
            this.reason = reason;
        }

        public string Role => this.role ?? throw new InvalidOperationException("Cannot get Role from failed target match result.");
        
        public string Reason => this.reason ?? throw new InvalidOperationException("Cannot get Reason from successful target match result.");

        public bool IsSuccess => this.role != null;

        public static TargetMatchResult Success(string role) => new TargetMatchResult(role, null);

        public static TargetMatchResult Failure(string reason) => new TargetMatchResult(null, reason);
    }
}
