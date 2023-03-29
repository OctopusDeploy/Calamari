using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class CronJob: Resource
    {
        public string Schedule { get; set; }
        public bool Suspend { get; set; }

        public CronJob(JObject json) : base(json)
        {
            Schedule = Field("$.spec.schedule");
            Suspend = FieldOrDefault("$.spec.suspend", false);
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<CronJob>(lastStatus);
            return last.Schedule != Schedule || last.Suspend != Suspend;
        }
    }
}

