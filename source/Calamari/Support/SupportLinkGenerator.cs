using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Support
{
    public class SupportLinkGenerator : ISupportLinkGenerator
    {
        private readonly Dictionary<string, string> SupportLinks = new Dictionary<string, string>()
        {
            {
                "JAVA", "http://g.octopushq.com/JavaAppDeploy"
            }
        };
       
        
        public string GenerateSupportMessage(string baseMessage, string errorCode)
        {          
            return SupportLinks
                .Where(entry => errorCode.StartsWith(entry.Key))
                .Select(entry => $"{errorCode}: {baseMessage} {entry.Value + "#" + errorCode.ToLower()}")
                .FirstOrDefault() ?? $"{errorCode}: {baseMessage}";
        }
    }
}