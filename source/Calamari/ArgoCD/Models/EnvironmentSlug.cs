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
    
    public static class EnvironmentSlugExtensionMethods
    {
        public static EnvironmentSlug ToEnvironmentSlug(this string value) => new EnvironmentSlug(value);
    }
}
