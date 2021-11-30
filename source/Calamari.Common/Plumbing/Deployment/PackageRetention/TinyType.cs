using System;
using System.Dynamic;
using YamlDotNet.Core.Tokens;

namespace Calamari.Deployment.PackageRetention
{
    //TODO: At some point, replace this basic tiny types implementation with the Octopus one.  The blocking issue is Net40 atm.
    public abstract class TinyType<T> where T : IComparable
    {
        public readonly T Value;

        protected TinyType(T value)
        {
            this.Value = value;
        }
        static object Create(Type type, T value)
        {
            return Activator.CreateInstance(type, value);
        }

        public static U Create<U>(T value) where U : TinyType<T>
        {
            return (U)Create(typeof(U), value);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;

            var other = (TinyType<T>)obj;

            return this.Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return GetType().GetHashCode() ^ (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator == (TinyType<T>? first, TinyType<T>? second)
        {
            if (first is null || second is null) return false;
            return first.Equals(second);
        }

        public static bool operator !=(TinyType<T> first, TinyType<T> second)
        {
            return !(first == second);
        }

        public override string ToString()
        {
            return Value?.ToString();
        }
    }
}