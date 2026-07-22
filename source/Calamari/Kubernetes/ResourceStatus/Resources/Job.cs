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

            if (!options.WaitForJobs)
            {
                ResourceStatus = ResourceStatus.Successful;
                return;
            }

            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus(succeeded, desired)
                : GetResourceStatus(json);
        }

        ResourceStatus GetLegacyResourceStatus(int succeeded, int desired)
        {
            var backoffLimit = FieldOrDefault("$.spec.backoffLimit", 0);

            // Using a default value of -1 rather than 0 as a job can be created with a backoffLimit of 0 and we don't want to immediately mark the job as failed
            var failed = FieldOrDefault("$.status.failed", -1);

            if (failed >= backoffLimit)
            {
                return ResourceStatus.Failed;
            }

            return succeeded == desired ? ResourceStatus.Successful : ResourceStatus.InProgress;
        }

        // Aligns with gitops-engine getJobHealth.
        static ResourceStatus GetResourceStatus(JObject json)
        {
            var conditions = json.SelectToken("$.status.conditions") as JArray ?? new JArray();
            var complete = false;
            var failed = false;
            foreach (var condition in conditions)
            {
                switch (condition["type"]?.ToString())
                {
                    case "Failed":
                        complete = true;
                        failed = true;
                        break;
                    // gitops-engine also treats a Suspended job as complete; both it and a completed job are non-blocking here.
                    case "Complete":
                    case "Suspended":
                        complete = true;
                        break;
                }
            }

            if (!complete)
            {
                return ResourceStatus.InProgress;
            }

            return failed ? ResourceStatus.Failed : ResourceStatus.Successful;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Job>(lastStatus);
            return last.Completions != Completions || last.Duration != Duration;
        }
    }
}
