using System;
using System.Linq;

namespace Calamari.ArgoCD.Git
{
    public static class UniqueRepoNameGenerator
    {
        public static string Generate()
        {
            var e = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            // Sanitise any non alpha or numeric characters
            return string.Join("", e.Where(char.IsLetterOrDigit));
        }
    }
}