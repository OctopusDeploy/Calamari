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
        private readonly IEnumerable<string> failureReasons;

        private TargetMatchResult(string role)
        {
            this.role = role;
            this.failureReasons = Enumerable.Empty<string>();
        }

        private TargetMatchResult(IEnumerable<string> failureReasons)
        {
            this.failureReasons = failureReasons;
        }

        public string Role => this.role ?? throw new InvalidOperationException("Cannot get Role from failed target match result.");
        
        public IEnumerable<string> FailureReasons => this.failureReasons;

        public bool IsSuccess => this.role != null;

        public static TargetMatchResult Success(string role) => new TargetMatchResult(role);

        public static TargetMatchResult Failure(IEnumerable<string> failureReasons) => new TargetMatchResult(failureReasons);
    }
}
