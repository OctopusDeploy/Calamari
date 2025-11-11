using System.Text.RegularExpressions;

namespace Calamari.ConsolidateCalamariPackages.Tests.TestModels
{
    public static class StringSanitisers
    {
       public static string Sanitise4PartVersions(this string s)
            => Regex.Replace(s, @"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+", "<version>");
        
        public static string SanitiseHash(this string s)
        {
            var result = Regex.Replace(s, "[a-z0-9]{32}", "<hash>");
            return result;
        }
    }
}