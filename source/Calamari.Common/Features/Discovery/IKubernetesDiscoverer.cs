using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Discovery
{
    public interface IKubernetesDiscoverer
    {
        string Name { get; }
        
        IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson);
    }
}