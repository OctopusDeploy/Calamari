using System;
using YamlDotNet.Core.Tokens;

namespace Calamari.Deployment.PackageRetention
{
    //TODO: Replace this basic tiny types implementation with the Octopus one.
    public abstract class CaseInsensitiveTinyType
    {
        public readonly string Value;

        public CaseInsensitiveTinyType(string value)
        {
            this.Value = value;
        }
        protected bool Equals(CaseInsensitiveTinyType other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;

            var other = (CaseInsensitiveTinyType)obj;

            return this == other;
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator == (CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            if (first is null || second is null) return false;
            return string.Equals(first?.Value, second?.Value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            return !(first == second);
        }
    }
}