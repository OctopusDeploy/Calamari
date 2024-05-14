using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public static class KubernetesVariableExtensions
    {
        public static bool IsKubernetesScript(this IVariables variables)
        {
            //A Kubernetes agent won't have any of the other variables set
            var isKubernetesAgentTarget = string.Equals(variables.Get(MachineVariables.DeploymentTargetType), "KubernetesTentacle", StringComparison.OrdinalIgnoreCase);
            
            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) ||
                                 !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            
            return isKubernetesAgentTarget || hasClusterUrl || hasClusterName;
        }
    }
}