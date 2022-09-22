using System;
using System.Diagnostics;

namespace Calamari.Terraform.Helpers
{
    public static class VersionExtensions
    {
        public static bool IsLessThan(this Version value, string compareTo)
        {
            return value.CompareTo(new Version(compareTo)) < 0;
        }
    }
}
