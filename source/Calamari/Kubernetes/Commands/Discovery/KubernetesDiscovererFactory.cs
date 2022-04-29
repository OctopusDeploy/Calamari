using System;

namespace Calamari.Kubernetes.Commands.Discovery
{
    public class KubernetesDiscovererFactory: IKubernetesDiscovererFactory
    {
        readonly Func<AzureKubernetesDiscoverer> azureKubernetesDiscovererFactory;

        public KubernetesDiscovererFactory(
            Func<AzureKubernetesDiscoverer> azureKubernetesDiscovererFactory)
        {
            this.azureKubernetesDiscovererFactory = azureKubernetesDiscovererFactory;
        }
        
        public bool TryGetKubernetesDiscoverer(string type, out IKubernetesDiscoverer discoverer)
        {
            switch (type)
            {
                case AzureKubernetesDiscoverer.AuthenticationContextTypeName:
                    discoverer = azureKubernetesDiscovererFactory();
                    return true;
                default:
                    discoverer = null;
                    return false;
                            
            }
        }
    }
}