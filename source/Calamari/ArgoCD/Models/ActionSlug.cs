using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class ActionSlug : CaseInsensitiveStringTinyType
    {
        public ActionSlug(string value) : base(value.Trim())
        {
        }
    }
    
    static class ActionSlugExtensionMethods
    {
        public static ActionSlug? ToActionSlug(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new ActionSlug(value.Trim());
    }
}
