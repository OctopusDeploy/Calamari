namespace Calamari.Kubernetes.ResourceStatus
{
    public class DeploymentContext
    {
        public string Cluster { get; set; }
        public string ActionId { get; set; }
        public int DeploymentTimeoutSeconds { get; set; }
        public int StabilizationTimeoutSeconds { get; set; }
    }
}