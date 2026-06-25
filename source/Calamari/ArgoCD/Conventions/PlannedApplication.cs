using System.Collections.Generic;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions
{
    // A single in-scope source of an application, paired with the work item that will write/commit/push it.
    public record PlannedSource(ApplicationSourceWithMetadata Source, RepositorySourceUpdate Update);

    // The result of inspecting one application: its parsed manifest, the gateway it belongs to, and the
    // in-scope sources that need processing. Built up-front for every application so that the sources can
    // be grouped by repository/branch across applications before any cloning happens.
    public class PlannedApplication
    {
        public PlannedApplication(ArgoCDApplicationDto application,
                                  ArgoCDGatewayDto gateway,
                                  Application applicationFromYaml,
                                  IReadOnlyList<PlannedSource> sources,
                                  int totalSourceCount,
                                  int matchingSourceCount)
        {
            Application = application;
            Gateway = gateway;
            ApplicationFromYaml = applicationFromYaml;
            Sources = sources;
            TotalSourceCount = totalSourceCount;
            MatchingSourceCount = matchingSourceCount;
        }

        public ArgoCDApplicationDto Application { get; }
        public ArgoCDGatewayDto Gateway { get; }
        public Application ApplicationFromYaml { get; }
        public NamespacedApplicationName NamespacedName
        {
            get
            {
                return ApplicationFromYaml.QualifiedName;
            }
        }

        public string ApplicationName
        {
            get
            {
                return ApplicationFromYaml.Metadata.Name;
            }
        }
        public IReadOnlyList<PlannedSource> Sources { get; }
        public int TotalSourceCount { get; }
        public int MatchingSourceCount { get; }
    }
}
