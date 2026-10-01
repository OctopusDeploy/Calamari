using System;

namespace Calamari.ArgoCD.Conventions
{
    public class GitCommitParameters
    {
        public const int DefaultPushRetryAttempts = 2;
        public const int MinPushRetryAttempts = 0;
        public const int MaxPushRetryAttempts = 10;

        public string Summary { get; }
        public string Description { get; }
        public bool RequiresPr { get; }
        public int PushRetryAttempts { get; }

        public GitCommitParameters(string summary, string description, bool requiresPr, int pushRetryAttempts = DefaultPushRetryAttempts)
        {
            Summary = summary;
            Description = description;
            RequiresPr = requiresPr;
            PushRetryAttempts = pushRetryAttempts;
        }

        // Clamps a user-supplied (and possibly missing) retry count to the supported range, falling back to the default when unset.
        public static int ClampPushRetryAttempts(int? value)
        {
            return Math.Clamp(value ?? DefaultPushRetryAttempts, MinPushRetryAttempts, MaxPushRetryAttempts);
        }
    }
}
