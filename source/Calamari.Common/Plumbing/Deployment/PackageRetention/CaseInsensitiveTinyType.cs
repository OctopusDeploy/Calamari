using System;
using System.Dynamic;
using YamlDotNet.Core.Tokens;

namespace Calamari.Deployment.PackageRetention
{
    public abstract class CaseInsensitiveTinyType : TinyType<string>
    {
        protected CaseInsensitiveTinyType(string value)
            : base(value)
        {
        }

        protected bool Equals(CaseInsensitiveTinyType other)
        {
            return Value == other.Value;
        }

        public static bool operator == (CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            if (first is null || second is null) return false;
            return string.Equals(first.Value, second.Value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(CaseInsensitiveTinyType first, CaseInsensitiveTinyType second)
        {
            return !(first == second);
        }
    }
}