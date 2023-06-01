using Calamari.Common.Events;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes
{
    public class KubectlResourcesAppliedEvent : InProcessEventBase<ResourceIdentifier[]>
    {
    }
}