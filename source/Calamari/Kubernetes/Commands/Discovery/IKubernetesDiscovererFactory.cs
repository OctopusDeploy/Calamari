using System;
using Calamari.Common.Features.Discovery;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public interface IKubernetesDiscovererFactory
    {
        bool TryGetKubernetesDiscoverer(string type, out IKubernetesDiscoverer discoverer);
    }
}