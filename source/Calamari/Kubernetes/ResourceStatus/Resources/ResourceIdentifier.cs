namespace Calamari.Kubernetes.ResourceStatus.Resources
{
   /// <summary>
   /// Identifies a unique resource in a kubernetes cluster
   /// </summary>
   public struct ResourceIdentifier : IResourceIdentity
   {
       // API version is irrelevant for identifying a resource,
       // since the resource name must be unique across all api versions.
       // https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#names
       public string Kind { get; }
       public string Name { get; }
       public string Namespace { get; }

       public ResourceIdentifier(string kind, string name, string @namespace)
       {
           Kind = kind;
           Name = name;
           Namespace = @namespace;
       }
   }
}