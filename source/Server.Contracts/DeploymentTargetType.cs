using System;

namespace Sashimi.Server.Contracts
{
    public class DeploymentTargetType : IEquatable<DeploymentTargetType>
    {
        public static readonly DeploymentTargetType Ssh = new DeploymentTargetType("Ssh", "SSH");

        public string Id { get; }
        public string DisplayName { get; }

        public DeploymentTargetType(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public bool Equals(DeploymentTargetType? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DeploymentTargetType) obj);
        }

        public override int GetHashCode()
            => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

        public static bool operator ==(DeploymentTargetType left, DeploymentTargetType right)
            => Equals(left, right);

        public static bool operator !=(DeploymentTargetType left, DeploymentTargetType right)
            => !Equals(left, right);
    }
}