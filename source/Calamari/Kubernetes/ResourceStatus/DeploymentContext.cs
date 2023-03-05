namespace Calamari.Kubernetes.ResourceStatus
{
    public class DeploymentContext
    {
        public string Cluster { get; set; }
        public string ActionId { get; set; }
        public int MaxTimeoutSeconds { get; set; }
        public int StabilizationTimeoutSeconds { get; set; }
    }
}