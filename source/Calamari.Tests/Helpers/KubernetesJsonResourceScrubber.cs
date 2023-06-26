using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Tests.Helpers
{
    public static class KubernetesJsonResourceScrubber
    {
        public static string ScrubRawJson(string json, Func<JProperty, bool> propertiesToRemovePredicate)
        {
            var jObject = JObject.Parse(json);

            jObject.Descendants()
                   .OfType<JProperty>()
                   .Where(propertiesToRemovePredicate)
                   .ToList()
                   .ForEach(p => p.Remove());

            return jObject.ToString();
        }
    }
}