using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes;
using ArgoCDFilesUpdatedAttributes = Calamari.Kubernetes.SpecialVariables.ServiceMessages.ArgoCDFilesUpdated.Attributes;

namespace Calamari.ArgoCD
{
    public interface IArgoCDFilesUpdatedReporter
    {
        void ReportDeployments(IReadOnlyList<ProcessApplicationResult> applicationResults);
    }

    public class ArgoCDFilesUpdatedReporter : IArgoCDFilesUpdatedReporter
    {
        readonly ILog log;

        public ArgoCDFilesUpdatedReporter(ILog log)
        {
            this.log = log;
        }

        public void ReportDeployments(IReadOnlyList<ProcessApplicationResult> applicationResults)
        {
            foreach (var appResult in applicationResults.Where(r => r.Updated))
            {
                var parameters = new Dictionary<string, string>
                {
                    { ArgoCDFilesUpdatedAttributes.GatewayId, appResult.GatewayId },
                    { ArgoCDFilesUpdatedAttributes.ApplicationName, appResult.ApplicationName.Value },
                    { ArgoCDFilesUpdatedAttributes.Sources, JsonSerializer.Serialize(appResult.UpdatedSourceDetails) }
                };

                var message = new ServiceMessage(
                    SpecialVariables.ServiceMessages.ArgoCDFilesUpdated.Name,
                    parameters);

                log.WriteServiceMessage(message);
            }
        }
    }
}
