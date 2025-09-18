using System;
using System.Text.RegularExpressions;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Mapping
{
    public partial class GoTemplatingToOctostacheConverter
    {
        public static string ConvertToOctostache(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return GoTemplatingValueSyntax().Replace(input, "#{$1}");
        }

        static Regex GoTemplatingValueSyntax()
        {
            return new Regex(@"\{\{\s*\.Values\.([^}]+?)\s*\}\}", RegexOptions.Compiled);
        }
    }
}