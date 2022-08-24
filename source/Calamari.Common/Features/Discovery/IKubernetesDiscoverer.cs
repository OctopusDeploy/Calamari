using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Discovery
{
    public interface IKubernetesDiscoverer
    {
        string Type { get; }

        IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson);
    }
}