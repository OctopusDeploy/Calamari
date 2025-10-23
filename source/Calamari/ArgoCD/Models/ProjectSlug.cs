using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class ProjectSlug : CaseInsensitiveStringTinyType
    {
        public ProjectSlug(string value) : base(value.Trim())
        {
        }
    }
    
    static class ProjectSlugExtensionMethods
    {
        public static ProjectSlug? ToProjectSlug(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new ProjectSlug(value.Trim());
    }
}
