using System;
using System.Dynamic;
using YamlDotNet.Core.Tokens;

namespace Calamari.Deployment.PackageRetention
{
    public abstract class CaseInsensitiveTinyType : TinyType<string>//, IEquatable<CaseInsensitiveTinyType>
    {
        protected CaseInsensitiveTinyType(string value)
            : base(value)
        {
        }
        /*

        public bool Equals(CaseInsensitiveTinyType? other)
        {
            return this == other;
        }     */

        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            if (obj is CaseInsensitiveTinyType ciTinyT)
                return string.Equals(ciTinyT.Value, Value, StringComparison.OrdinalIgnoreCase);;

            return false;
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode() ^ (Value != null ? Value.ToLowerInvariant().GetHashCode() : 0);
        }

        public static bool operator ==(CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            if (first is null || second is null) return false;
            return first.Equals(second);
        }

        public static bool operator !=(CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            return !(first == second);
        }
    }
}