using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Web;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureHomeLinksContributor : IHomeLinksContributor
    {
        static readonly IDictionary<string, string> Links;

        static AzureHomeLinksContributor()
        {
            Links = new Dictionary<string, string>
            {
                { "AzureEnvironments", $"~{AzureApi.AzureEnvironmentsPath}" }
            };
        }

        public IDictionary<string, string> GetLinksToContribute()
        {
            return Links;
        }
    }
}