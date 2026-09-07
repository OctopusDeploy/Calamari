using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class StepSlug : CaseInsensitiveStringTinyType
    {
        public StepSlug(string value) : base(value.Trim())
        {
        }
    }
    
    static class StepSlugExtensionMethods
    {
        public static StepSlug? ToStepSlug(this string? value) => string.IsNullOrWhiteSpace(value) ? null : new StepSlug(value.Trim());
    }
}
