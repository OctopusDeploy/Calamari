using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class EnvironmentSlug : CaseInsensitiveStringTinyType
    {
        public EnvironmentSlug(string value) : base(value.Trim())
        {
        }
    }
    
    static class EnvironmentSlugExtensionMethods
    {
        public static EnvironmentSlug? ToEnvironmentSlug(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new EnvironmentSlug(value.Trim());
    }
}
