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
    
    public static class TenantSlugExtensionMethods
    {
        public static TenantSlug ToTenantSlug(this string value) => new TenantSlug(value);
    }
}
