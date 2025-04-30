using System;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Job: Resource
    {
        public string Completions { get; }
        public string Duration { get; }

        public Job(JObject json, Options options) : base(json, options)
        {
            var succeeded = FieldOrDefault("$.status.succeeded", 0);
            var desired = FieldOrDefault("$.spec.completions", 0);
            Completions = $"{succeeded}/{desired}";

            var defaultTime = DateTime.UtcNow;
            var completionTime = FieldOrDefault("$.status.completionTime", defaultTime);
            var startTime = FieldOrDefault("$.status.startTime", defaultTime);

            Duration = $"{completionTime - startTime:c}";

            var backoffLimit = FieldOrDefault("$.spec.backoffLimit", 0);

            // Using a default value of -1 rather than 0 as a job can be created with a backoffLimit of 0 and we don't want to immediately mark the job as failed
            var failed = FieldOrDefault("$.status.failed", -1);

            if (!options.WaitForJobs)
            {
                ResourceStatus = ResourceStatus.Successful;
                return;
            }

            if (failed >= backoffLimit)
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

