using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Models
{
    public class ApplicationName : CaseSensitiveStringTinyType
    {
        public ApplicationName(string value) : base(value)
        {
        }
    }
    
    public static class ApplicationNameExtensionMethods
    {
        public static ApplicationName ToApplicationName(this string value) => string.IsNullOrWhiteSpace(value) ? null : new ApplicationName(value.Trim());
    }
}