#if !NET40
using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IKubernetesApplyExecutor
    {
        Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null);
    }
}
#endif