using System;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
   /// <summary>
   /// Identifies a unique resource in a kubernetes cluster
   /// </summary>
   public struct ResourceIdentifier : IResourceIdentity, IEquatable<ResourceIdentifier>
   {
       // API version is irrelevant for identifying a resource,
       // since the resource name must be unique across all api versions.
       // https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#names
       public string Group { get; }
       public string Kind { get; }
       public string Name { get; }
       public string Namespace { get; }

       public ResourceIdentifier(string group, string kind, string name, string @namespace)
       {
           Group = group;
           Kind = kind;
           Name = name;
           Namespace = @namespace;
       }

       public bool Equals(ResourceIdentifier other)
       {
           return Group == other.Group
                  && Kind == other.Kind
                  && Name == other.Name
                  && Namespace == other.Namespace;
       }

       public override bool Equals(object obj)
       {
           return obj is ResourceIdentifier other && Equals(other);
       }

       public override int GetHashCode()
       {
           unchecked
           {
               var hashCode = (Group != null ? Group.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Kind != null ? Kind.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Namespace != null ? Namespace.GetHashCode() : 0);
               return hashCode;
           }
       }
   }
}