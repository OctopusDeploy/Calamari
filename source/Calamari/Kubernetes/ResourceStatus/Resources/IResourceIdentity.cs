namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public interface IResourceIdentity
    {
        ResourceGroupVersionKind GroupVersionKind { get; }
        string Name { get; }
        string Namespace { get; }
    }
}