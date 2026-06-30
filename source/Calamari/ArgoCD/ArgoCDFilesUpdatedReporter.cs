using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Conventions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes;
using Octopus.Calamari.Contracts.ArgoCD;
using ArgoCDFilesUpdatedAttributes = Octopus.Calamari.Contracts.ArgoCD.ServiceMessages.ArgoCDFilesUpdated.Attributes;

namespace Calamari.ArgoCD
{
    public interface IArgoCDFilesUpdatedReporter
    {
        void ReportFilesUpdated(GitCommitParameters gitCommitParameters, IReadOnlyList<ProcessApplicationResult> applicationResults);
    }

    public class ArgoCDFilesUpdatedReporter(ILog log) : IArgoCDFilesUpdatedReporter
    {
        public void ReportFilesUpdated(GitCommitParameters gitCommitParameters, IReadOnlyList<ProcessApplicationResult> applicationResults)
        {
            // if we are creating a pull request, we don't want to report files updated (as this will be passed down as output variables _with_ the PR info)
            // See ArgoCDOutputVariablesWriter
            if (gitCommitParameters.RequiresPr)
            {
                return;
            }
            
            //file paths _must_ use forward slashes for directory separators
            foreach (var appResult in applicationResults.Where(r => r.Tracked))
            {
                var parameters = new Dictionary<string, string>
                {
                    { ArgoCDFilesUpdatedAttributes.GatewayId, appResult.GatewayId },
                    { ArgoCDFilesUpdatedAttributes.ApplicationName, appResult.ApplicationName.Value },
                    { ArgoCDFilesUpdatedAttributes.Sources, JsonSerializer.Serialize(appResult.TrackedSourceDetails.Select(MapSource).ToList()) }
                };

                var message = new ServiceMessage(
                                                 ServiceMessages.ArgoCDFilesUpdated.Name,
                                                 parameters);

                log.WriteServiceMessage(message);
            }
        }

        static SourceFileChanges MapSource(TrackedSourceDetail trackedSourceDetail)
        {
            return new SourceFileChanges(
                trackedSourceDetail.CommitSha,
                trackedSourceDetail.CommitTimestamp,
                trackedSourceDetail.SourceIndex,
                trackedSourceDetail.ReplacedFiles.Select(f => f with
                {
                    FilePath = f.FilePath.EnsurePosixDirectorySeparator()
                }).ToList(),
                trackedSourceDetail.PatchedFiles.Select(f => f with
                {
                    FilePath = f.FilePath.EnsurePosixDirectorySeparator()
                }).ToList()
            );
        }
    }
}