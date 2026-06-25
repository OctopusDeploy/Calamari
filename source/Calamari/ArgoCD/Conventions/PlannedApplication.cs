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
                                  IReadOnlyList<PlannedSource> sources,
                                  int totalSourceCount)
        {
            Application = application;
            Gateway = gateway;
            Sources = sources;
            TotalSourceCount = totalSourceCount;
        }

        public ArgoCDApplicationDto Application { get; }
        public ArgoCDGatewayDto Gateway { get; }
        public NamespacedApplicationName NamespacedName
        {
            get
            {
                return NamespacedApplicationName.Create(ApplicationName, ApplicationNamespace);
            }
        }

        public string ApplicationName
        {
            get
            {
                return Application.Name;
            }
        }

        public string ApplicationNamespace
        {
            get
            {
                return Application.KubernetesNamespace;
            }
        }

        public IReadOnlyList<PlannedSource> Sources { get; }
        public int TotalSourceCount { get; }
        public int MatchingSourceCount
        {
            get
            {
                return Sources.Count;
            }
        }
    }
}
