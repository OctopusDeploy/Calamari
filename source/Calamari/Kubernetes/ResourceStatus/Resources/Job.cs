using System;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Job: Resource
    {
        public string Completions { get; }
        public string Duration { get; }

        public override ResourceStatus ResourceStatus { get; }

        public Job(JObject json) : base(json)
        {
            var succeeded = FieldOrDefault("$.status.succeeded", 0);
            var desired = FieldOrDefault("$.spec.completions", 0);
            Completions = $"{succeeded}/{desired}";

            var defaultTime = DateTime.Now;
            var completionTime = FieldOrDefault("$.status.completionTime", defaultTime);
            var startTime = FieldOrDefault("$.status.startTime", defaultTime);

            Duration = $"{completionTime - startTime:c}";

            var backoffLimit = FieldOrDefault("$.spec.backoffLimit", 0);
            var failed = FieldOrDefault("$.status.failed", 0);

            if (backoffLimit != 0 && failed == backoffLimit)
            {
                ResourceStatus = ResourceStatus.Failed;
            } 
            else if (succeeded == desired)
            {
                ResourceStatus = ResourceStatus.Successful;
            }
            else
            {
                ResourceStatus = ResourceStatus.InProgress;
            }
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Job>(lastStatus);
            return last.Completions != Completions || last.Duration != Duration;
        }
    }
}

