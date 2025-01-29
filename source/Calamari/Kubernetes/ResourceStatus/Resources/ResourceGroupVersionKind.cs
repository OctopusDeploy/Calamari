using System;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ResourceGroupVersionKind : IEquatable<ResourceGroupVersionKind>
    {
        public ResourceGroupVersionKind(string group, string version, string kind)
        {
            Group = group;
            Version = version;
            Kind = kind;
        }

        public string Group { get; }
        public string Version { get; }
        public string Kind { get; }
        public bool Equals(ResourceGroupVersionKind other)
        {
            return other != null
                   && Group == other.Group
                   && Version == other.Version
                   && Kind == other.Kind;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ResourceGroupVersionKind other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Group != null ? Group.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Kind != null ? Kind.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return !Group.IsNullOrEmpty() ? $"{Group}/{Version}/{Kind}" : $"{Version}/{Kind}";
        }
    }
}