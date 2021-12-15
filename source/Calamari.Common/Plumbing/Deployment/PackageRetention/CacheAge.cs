using System;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class CacheAge : IComparable<CacheAge>, IEquatable<CacheAge>
    {
        public int Value { get; private set; }

        [JsonConstructor]
        public CacheAge(int value)
        {
            Value = value;
        }

        public void IncrementAge()
        {
            Value++;
        }

        public bool Equals(CacheAge? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((CacheAge)obj);
        }

        public override int GetHashCode()
        {
            return Value;
        }   

        public int CompareTo(CacheAge? other)
        {
            if (ReferenceEquals(this, other))
                return 0;
            if (ReferenceEquals(null, other))
                return 1;
            return Value.CompareTo(other.Value);
        }

        public static bool operator > (CacheAge first, CacheAge second)
        {
            return first.Value > second.Value;
        }

        public static bool operator < (CacheAge first, CacheAge second)
        {
            return first.Value < second.Value;
        }

        public static bool operator == (CacheAge first, CacheAge second)
        {
            return first.Value == second.Value;
        }

        public static bool operator !=(CacheAge first, CacheAge second)
        {
            return first.Value != second.Value;
        }
    }
}