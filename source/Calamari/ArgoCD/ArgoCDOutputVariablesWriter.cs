using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD
{
    public class ArgoCDOutputVariablesWriter
    {
        readonly ILog log;

        public ArgoCDOutputVariablesWriter(ILog log)
        {
            this.log = log;
        }

        public void WriteImageUpdateOutput(IEnumerable<string> gateways,
                                           IEnumerable<string> gitRepos,
                                           IReadOnlyCollection<(ApplicationName ApplicationName, int TotalSourceCount, int MatchingSourceCount)> totalApplicationsWithSourceCounts,
                                           IReadOnlyCollection<(ApplicationName ApplicationName, int SourceCount)> updatedApplicationsWithSourceCounts,
                                           int imagesUpdatedCount)
        {
            WriteGatewayIds(gateways);
            WriteGitUris(gitRepos);
            WriteTotalApplicationsWithSourceCounts(totalApplicationsWithSourceCounts);
            WriteUpdatedApplicationsWithSourceCounts(updatedApplicationsWithSourceCounts);
            
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedImages, imagesUpdatedCount.ToString());
        }

        public void WriteManifestUpdateOutput(IEnumerable<string> gateways,
                                              IEnumerable<string> gitRepos,
                                              IReadOnlyCollection<(ApplicationName ApplicationName, int TotalSourceCount, int MatchingSourceCount)> totalApplicationsWithSourceCounts,
                                              IReadOnlyCollection<(ApplicationName ApplicationName, int SourceCount)> updatedApplicationsWithSourceCounts)
        {
            WriteGatewayIds(gateways);
            WriteGitUris(gitRepos);
            WriteTotalApplicationsWithSourceCounts(totalApplicationsWithSourceCounts);
            WriteUpdatedApplicationsWithSourceCounts(updatedApplicationsWithSourceCounts);
        }

        void WriteTotalApplicationsWithSourceCounts(IReadOnlyCollection<(ApplicationName ApplicationName, int TotalSourceCount, int MatchingSourceCount)> matchingApplicationsWithSourceCounts)
        {
            var totalApps = ToCommaSeparatedString(matchingApplicationsWithSourceCounts.Select(c => c.ApplicationName));
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.MatchingApplications, totalApps);

            var totalSourceCounts = ToCommaSeparatedString(matchingApplicationsWithSourceCounts.Select(c => c.TotalSourceCount));
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.MatchingApplicationTotalSourceCounts, totalSourceCounts);
            
            var matchingSourceCounts = ToCommaSeparatedString(matchingApplicationsWithSourceCounts.Select(c => c.MatchingSourceCount));
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.MatchingApplicationMatchingSourceCounts, matchingSourceCounts);
        }

        void WriteUpdatedApplicationsWithSourceCounts(IReadOnlyCollection<(ApplicationName ApplicationName, int SourceCount)> updatedApplicationsWithSourceCount)
        {
            var updatedApps = ToCommaSeparatedString(updatedApplicationsWithSourceCount.Select(c => c.ApplicationName));
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedApplications, updatedApps);
            var updatedSourceCounts = ToCommaSeparatedString(updatedApplicationsWithSourceCount.Select(c => c.SourceCount));

            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedApplicationSourceCounts, updatedSourceCounts);
        }

        void WriteGitUris(IEnumerable<string> gitRepos)
        {
            var gitUris = ToCommaSeparatedString(gitRepos);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GitUris, gitUris);
        }

        void WriteGatewayIds(IEnumerable<string> gateways)
        {
            var gatewayIds = ToCommaSeparatedString(gateways);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GatewayIds, gatewayIds);
        }

        static string ToCommaSeparatedString<T>(IEnumerable<T> items)
        {
            return string.Join(", ", items);
        }
    }
}
