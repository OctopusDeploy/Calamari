using System.Collections.Generic;
using System.IO;
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
        void ReportFilesUpdated(IReadOnlyList<ProcessApplicationResult> applicationResults);
    }

    public class ArgoCDFilesUpdatedReporter : IArgoCDFilesUpdatedReporter
    {
        readonly ILog log;

        public ArgoCDFilesUpdatedReporter(ILog log)
        {
            this.log = log;
        }

        public void ReportFilesUpdated(IReadOnlyList<ProcessApplicationResult> applicationResults)
        {
            //file paths _must_ use forward slashes for directory separators
            foreach (var appResult in applicationResults.Where(r => r.Updated))
            {
                var parameters = new Dictionary<string, string>
                {
                    { ArgoCDFilesUpdatedAttributes.GatewayId, appResult.GatewayId },
                    { ArgoCDFilesUpdatedAttributes.ApplicationName, appResult.ApplicationName.Value },
                    { ArgoCDFilesUpdatedAttributes.Sources, JsonSerializer.Serialize(ConvertPathsToPosix(appResult.UpdatedSourceDetails)) }
                };

                var message = new ServiceMessage(
                                                 SpecialVariables.ServiceMessages.ArgoCDFilesUpdated.Name,
                                                 parameters);

                log.WriteServiceMessage(message);
            }
        }

        List<UpdatedSourceDetail> ConvertPathsToPosix(List<UpdatedSourceDetail> inputs)
        {
            return inputs.Select(usd => usd with
                         {
                             ReplacedFiles = usd.ReplacedFiles.Select(rf => rf with
                                                {
                                                    FilePath = rf.FilePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                })
                                                .ToList(),
                             PatchedFiles = usd.PatchedFiles.Select(pf => pf with
                                               {
                                                   FilePath = pf.FilePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                               })
                                               .ToList()
                         })
                         .ToList();
        }
    }
}