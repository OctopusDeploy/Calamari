using System;
using System.Collections.Generic;
using System.Linq;
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
                                           IEnumerable<string> totalApplications,
                                           IEnumerable<string> updatedApplications,
                                           int imagesUpdatedCount)
        {
            var gatewayIds = ToCommaSeparatedString(gateways);
            var gitUris = ToCommaSeparatedString(gitRepos);
            var totalApps = ToCommaSeparatedString(totalApplications);
            var updatedApps = ToCommaSeparatedString(updatedApplications);

            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GatewayIds, gatewayIds);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GitUris, gitUris);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.MatchingApplications, totalApps);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedApplications, updatedApps);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedImages, imagesUpdatedCount.ToString());

        }
        
        public void WriteManifestUpdateOutput(IEnumerable<string> gateways,
                                           IEnumerable<string> gitRepos,
                                           IEnumerable<string> totalApplications,
                                           IEnumerable<string> updatedApplications)
        {
            var gatewayIds = ToCommaSeparatedString(gateways);
            var gitUris = ToCommaSeparatedString(gitRepos);
            var totalApps = ToCommaSeparatedString(totalApplications);
            var updatedApps = ToCommaSeparatedString(updatedApplications);

            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GatewayIds, gatewayIds);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.GitUris, gitUris);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.MatchingApplications, totalApps);
            log.SetOutputVariableButDoNotAddToVariables(SpecialVariables.Git.Output.UpdatedApplications, updatedApps);
        }

        static string ToCommaSeparatedString(IEnumerable<string> items)
        {
            return string.Join(", ", items);
        }
    }
}
