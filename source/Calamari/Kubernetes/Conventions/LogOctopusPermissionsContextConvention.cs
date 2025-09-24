using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Conventions
{
    /// <summary>
    /// Logs out the value of
    /// </summary>
    public class LogOctopusPermissionsContextConvention : IInstallConvention
    {
        public const string OpcPermissionsContext = "OpcPermissionsContext";

        readonly ILog log;

        public LogOctopusPermissionsContextConvention(ILog log)
        {
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            var value = Environment.GetEnvironmentVariable(OpcPermissionsContext);

            if (!string.IsNullOrEmpty(value))
            {
                var parsed = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(value), Formatting.Indented);
                log.Verbose($"Octopus permissions context:");
                log.Verbose(parsed);
            }
        }
    }
}