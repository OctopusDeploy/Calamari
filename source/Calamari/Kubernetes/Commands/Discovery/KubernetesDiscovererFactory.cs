using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public class KubernetesDiscovererFactory: IKubernetesDiscovererFactory
    {
        readonly IDictionary<string, IKubernetesDiscoverer> discoverers;

        public KubernetesDiscovererFactory(IEnumerable<IKubernetesDiscoverer> discoverers)
        {
            this.discoverers = discoverers.ToDictionary(x => x.Type, x => x);
        }
        
        public bool TryGetKubernetesDiscoverer(string type, out IKubernetesDiscoverer discoverer)
        {
            return discoverers.TryGetValue(type, out discoverer);
        }
    }
}