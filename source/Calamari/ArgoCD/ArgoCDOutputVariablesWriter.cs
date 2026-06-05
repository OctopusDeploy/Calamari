using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes;
using Octopus.Calamari.Contracts.Git;
using Octopus.CoreUtilities.Extensions;
using PullRequestCreatedServiceMessage = Octopus.Calamari.Contracts.Git.ServiceMessages.PullRequestCreated;

namespace Calamari.ArgoCD
{
    public class ArgoCDOutputVariablesWriter
    {
        readonly ILog log;

        public ArgoCDOutputVariablesWriter(ILog log)
        {
            this.log = log;
        }

        public void WriteSourceUpdateResultOutputWhenPushResultExists(
            string gatewayName,
            QualifiedApplicationName applicationName,
            int sourceIndex,
            SourceUpdateResult sourceUpdateResult)
        {
            var pushResult = sourceUpdateResult.PushResult;
            if (pushResult is null)
            {
                return;
            }

            var appSourceVariables = SpecialVariables.ArgoCD.Output
                                                     .Actions()
                                                     .ArgoCDGateways(gatewayName)
                                                     .Applications(applicationName)
                                                     .Sources(sourceIndex);

            log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.CommitSha, pushResult.CommitSha);
            log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.ShortSha, pushResult.ShortSha);
            log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.CommitTimestamp, pushResult.CommitTimestamp.ToString("O"));

            if (pushResult is PullRequestPushResult prResult)
            {
//                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.RepositoryUrl, prResult.RepositoryUri);
                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.PullRequestTitle, prResult.PullRequestTitle);
                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.PullRequestUrl, prResult.PullRequestUri);
                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.PullRequestNumber, prResult.PullRequestNumber.ToString(CultureInfo.InvariantCulture));

                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.PullRequestReplacedFiles,
                                                            JsonSerializer.Serialize(sourceUpdateResult.ReplacedFiles.Select(rf => rf with
                                                                                                       {
                                                                                                           FilePath = rf.FilePath.EnsurePosixDirectorySeparator()
                                                                                                       })
                                                                                                       .ToList()));

                log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.PullRequestPatchedFiles,
                                                            JsonSerializer.Serialize(sourceUpdateResult.PatchedFiles.Select(rf => rf with
                                                                                                       {
                                                                                                           FilePath = rf.FilePath.EnsurePosixDirectorySeparator()
                                                                                                       })
                                                                                                       .ToList()));
                
                var message = CreatePullRequestCreatedServiceMessage(prResult);

                log.WriteServiceMessage(message);
            }
        }

        static ServiceMessage CreatePullRequestCreatedServiceMessage(PullRequestPushResult prResult)
        {
            var parameters = new Dictionary<string, string>
            {
                { PullRequestCreatedServiceMessage.Attributes.PullRequestUri, prResult.PullRequestUri },
                { PullRequestCreatedServiceMessage.Attributes.RepositoryUri, prResult.RepositoryUri },
                { PullRequestCreatedServiceMessage.Attributes.VendorName, prResult.VendorName },
                { PullRequestCreatedServiceMessage.Attributes.SourceType, PullRequestCreatedServiceMessage.SourceTypes.ArgoCD },
            };

            var message = new ServiceMessage(
                                             PullRequestCreatedServiceMessage.Name,
                                             parameters);
            return message;
        }

        public void WriteImageUpdateOutput(List<ProcessApplicationResult> applicationResults)
        {
            
            foreach (var applicationResult in applicationResults)
            {
                foreach (var sourceDetail in applicationResult.TrackedSourceDetails)
                {
                    var appSourceVariables = SpecialVariables.ArgoCD.Output
                                                             .Actions()
                                                             .ArgoCDGateways(applicationResult.GatewayName)
                                                             .Applications(applicationResult.ApplicationName)
                                                             .Sources(sourceDetail.SourceIndex);
                    
                    log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.Updated, sourceDetail.CommitSha.IsNullOrEmpty().ToString());
                    log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.RepositoryUrl, sourceDetail.RepositoryUri);
                    log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.UpdatedImages, ToCommaSeparatedString(sourceDetail.imagesUpdated));
                }
            }
        }

        public void WriteManifestUpdateOutput(IReadOnlyCollection<ProcessApplicationResult> applicationResults)
        {
            foreach (var applicationResult in applicationResults)
            {
                foreach (var sourceDetail in applicationResult.TrackedSourceDetails)
                {
                    var appSourceVariables = SpecialVariables.ArgoCD.Output
                                                             .Actions()
                                                             .ArgoCDGateways(applicationResult.GatewayName)
                                                             .Applications(applicationResult.ApplicationName)
                                                             .Sources(sourceDetail.SourceIndex);
                    
                    log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.Updated, sourceDetail.CommitSha.IsNullOrEmpty().ToString());
                    log.SetOutputVariableButDoNotAddToVariables(appSourceVariables.RepositoryUrl, sourceDetail.RepositoryUri);
                }
            }
        }

        static string ToCommaSeparatedString<T>(IEnumerable<T> items)
        {
            return string.Join(", ", items);
        }
    }
}