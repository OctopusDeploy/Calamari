using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Discovery
{
    public interface IKubernetesDiscoverer
    {
        string Name { get; }
        
        IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson, IVariables variables);
    }
}