using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class ApplicationSourceName : CaseSensitiveStringTinyType
    {
        public ApplicationSourceName(string value) : base(value)
        {
        }
    }
    
    static class ApplicationSourceNameExtensionMethods
    {
        public static ApplicationSourceName? ToApplicationSourceName(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new ApplicationSourceName(value.Trim());
    }
}
