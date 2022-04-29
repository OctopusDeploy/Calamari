using System;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public interface IKubernetesDiscovererFactory
    {
        bool TryGetKubernetesDiscoverer(string type, out IKubernetesDiscoverer discoverer);
    }
}