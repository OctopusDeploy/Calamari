namespace Calamari.Common.Plumbing
{
    public static class KubernetesEnvironmentVariables
    {
        public const string KubernetesAgentNamespace = "OCTOPUS__K8STENTACLE__NAMESPACE";
        public const string KubernetesAgentVolumeFreeBytes = "OCTOPUS__K8STENTACLE__PERSISTENTVOLUMEFREEBYTES";
        public const string KubernetesAgentVolumeTotalBytes = "OCTOPUS__K8STENTACLE__PERSISTENTVOLUMETOTALBYTES";
    }
}