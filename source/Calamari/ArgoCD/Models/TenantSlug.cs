using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class TenantSlug : CaseInsensitiveStringTinyType
    {
        public TenantSlug(string value) : base(value.Trim())
        {
        }
    }
    
    static class TenantSlugExtensionMethods
    {
        public static TenantSlug? ToTenantSlug(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new TenantSlug(value.Trim());
    }
}
