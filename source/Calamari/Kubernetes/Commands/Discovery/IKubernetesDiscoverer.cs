using System;
using System.Collections.Generic;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public interface IKubernetesDiscoverer
    {
        IEnumerable<Cluster> DiscoveryClusters(string contextJson);
    }
}