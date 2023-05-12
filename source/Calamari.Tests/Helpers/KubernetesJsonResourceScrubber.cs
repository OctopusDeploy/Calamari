using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Tests.Helpers
{
    public static class KubernetesJsonResourceScrubber
    {
        public static string ScrubRawJson(string json)
        {
            var jObject = JObject.Parse(json);

            jObject.Descendants()
                   .OfType<JProperty>()
                   .Where(p => p.Name.Contains("time", StringComparison.InvariantCultureIgnoreCase) ||
                       p.Name == "annotations")
                   .ToList()
                   .ForEach(p => p.Remove());

            return jObject.ToString();
        }
    }
}