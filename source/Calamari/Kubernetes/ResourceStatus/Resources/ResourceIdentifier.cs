using System;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
   /// <summary>
   /// Identifies a unique resource in a kubernetes cluster
   /// </summary>
   public class ResourceIdentifier
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

       protected bool Equals(ResourceIdentifier other)
       {
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
           return HashCode.Combine(Kind, Name, Namespace);
       }

       public static bool operator ==(ResourceIdentifier left, ResourceIdentifier right)
       {
           return Equals(left, right);
       }

       public static bool operator !=(ResourceIdentifier left, ResourceIdentifier right)
       {
           return !Equals(left, right);
       }

       public static bool Equals(ResourceIdentifier x, ResourceIdentifier y)
       {
           if (ReferenceEquals(x, y)) return true;
           if (ReferenceEquals(x, null)) return false;
           if (ReferenceEquals(y, null)) return false;
           if (x.GetType() != y.GetType()) return false;
           return x.Kind == y.Kind && x.Name == y.Name && x.Namespace == y.Namespace;
       }

       public int GetHashCode(ResourceIdentifier obj)
       {
           return HashCode.Combine(obj.Kind, obj.Name, obj.Namespace);
       }
   }
}