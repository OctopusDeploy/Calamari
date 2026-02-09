using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes;
using ArgoCDDeploymentAttributes = Calamari.Kubernetes.SpecialVariables.ServiceMessages.ArgoCDDeployment.Attributes;

namespace Calamari.ArgoCD
{
    public interface IArgoCDDeploymentReporter
    {
        void ReportDeployments(IReadOnlyList<ProcessApplicationResult> applicationResults);
    }

    public class ArgoCDDeploymentReporter : IArgoCDDeploymentReporter
    {
        readonly ILog log;

        public ArgoCDDeploymentReporter(ILog log)
        {
            this.log = log;
        }

        public void ReportDeployments(IReadOnlyList<ProcessApplicationResult> applicationResults)
        {
            foreach (var appResult in applicationResults.Where(r => r.Updated))
            {
                var parameters = new Dictionary<string, string>
                {
                    { ArgoCDDeploymentAttributes.GatewayId, appResult.GatewayId },
                    { ArgoCDDeploymentAttributes.ApplicationName, appResult.ApplicationName.Value },
                    { ArgoCDDeploymentAttributes.Sources, JsonSerializer.Serialize(appResult.UpdatedSourceDetails) }
                };

                var message = new ServiceMessage(
                    SpecialVariables.ServiceMessages.ArgoCDDeployment.Name,
                    parameters);

                log.WriteServiceMessage(message);
            }
        }
    }
}
