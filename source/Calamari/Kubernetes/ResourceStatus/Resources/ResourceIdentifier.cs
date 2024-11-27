using System;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
   /// <summary>
   /// Identifies a unique resource in a kubernetes cluster
   /// </summary>
   public struct ResourceIdentifier : IResourceIdentity, IEquatable<ResourceIdentifier>
   {
       public ResourceGroupVersionKind GroupVersionKind { get; }
       public string Name { get; }
       public string Namespace { get; }

       public ResourceIdentifier(ResourceGroupVersionKind groupVersionKind, string name, string @namespace)
       {
           GroupVersionKind = groupVersionKind;
           Name = name;
           Namespace = @namespace;
       }

       public bool Equals(ResourceIdentifier other)
       {
           return GroupVersionKind.Equals(other.GroupVersionKind)
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
               var hashCode = (GroupVersionKind != null ? GroupVersionKind.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
               hashCode = (hashCode * 397) ^ (Namespace != null ? Namespace.GetHashCode() : 0);
               return hashCode;
           }
       }
   }
}