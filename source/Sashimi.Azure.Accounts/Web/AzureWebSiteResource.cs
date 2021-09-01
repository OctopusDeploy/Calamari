using System;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureWebSiteResource
    {
        AzureWebSiteResource(string name, string region, string resourceGroup)
        {
            Name = name;
            Region = region;
            ResourceGroup = resourceGroup;
        }

        public string Name { get; }

        // ReSharper disable once MemberCanBePrivate.Local this must be public or the serialization to the client doesn't work
        public string ResourceGroup { get; }

        public string Region { get; }

        public static AzureWebSiteResource ForResourceManagement(string name, string resourceGroup, string region)
        {
            return new(name, region, resourceGroup);
        }
    }
}