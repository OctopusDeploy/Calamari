using System;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
   /// <summary>
   /// Identifies a unique resource in a kubernetes cluster
   /// </summary>
   public class ResourceIdentifier : IEquatable<ResourceIdentifier>
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

       public bool Equals(ResourceIdentifier other)
       {
           if (ReferenceEquals(null, other)) return false;
           if (ReferenceEquals(this, other)) return true;
           return Kind == other.Kind && Name == other.Name && Namespace == other.Namespace;
       }

       public override bool Equals(object obj)
       {
           if (ReferenceEquals(null, obj)) return false;
           if (ReferenceEquals(this, obj)) return true;
           if (obj.GetType() != this.GetType()) return false;
           return Equals((ResourceIdentifier)obj);
       }

       public override int GetHashCode()
       {
           unchecked
           {
               var hashCode = (Kind != null ? Kind.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Namespace != null ? Namespace.GetHashCode() : 0);
               return hashCode;
           }
       }

       public static bool operator ==(ResourceIdentifier left, ResourceIdentifier right)
       {
           return Equals(left, right);
       }

       public static bool operator !=(ResourceIdentifier left, ResourceIdentifier right)
       {
           return !Equals(left, right);
       }
   }
}