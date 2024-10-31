using System;
using Azure.Core;
using Azure.ResourceManager.AppService;

#nullable enable
namespace Calamari.AzureAppService.Azure
{
    public class AzureTargetSite
    {
        public string SubscriptionId { get; }
        public string ResourceGroupName { get; }
        public string Site { get; }
        public string? Slot { get; }

        public string SiteAndSlot => HasSlot ? $"{Site}/{Slot}" : Site;
        public string ScmSiteAndSlot => HasSlot ? $"{Site}-{Slot}" : Site;

        public bool HasSlot => !string.IsNullOrWhiteSpace(Slot);

        public AzureTargetSite(string subscriptionId, string resourceGroupName, string siteAndMaybeSlotName, string? slotName = null)
        {
            SubscriptionId = subscriptionId;
            ResourceGroupName = resourceGroupName;

            var (parsedSiteName, parsedSlotName) = ParseSiteAndSlotName(siteAndMaybeSlotName, slotName);
            Site = parsedSiteName;
            Slot = parsedSlotName;
        }

        static (string ParsedSiteName, string? ParsedSlotName) ParseSiteAndSlotName(string siteAndMaybeSlotName, string? slotName)
        {
            string parsedSiteName;
            string? parsedSlotName;
            if (siteAndMaybeSlotName.Contains("("))
            {
                // legacy site and slot "site(slot)"
                var parenthesesIndex = siteAndMaybeSlotName.IndexOf("(", StringComparison.Ordinal);
                parsedSiteName = siteAndMaybeSlotName.Substring(0, parenthesesIndex).Trim();
                parsedSlotName = siteAndMaybeSlotName.Substring(parenthesesIndex + 1).Replace(")", string.Empty).Trim();
            }
            else if (siteAndMaybeSlotName.Contains("/"))
            {
                // "site/slot"
                var slashIndex = siteAndMaybeSlotName.IndexOf("/", StringComparison.Ordinal);
                parsedSiteName = siteAndMaybeSlotName.Substring(0, slashIndex).Trim();
                parsedSlotName = siteAndMaybeSlotName.Substring(slashIndex + 1).Trim();
            }
            else
            {
                parsedSiteName = siteAndMaybeSlotName;
                parsedSlotName = slotName;
            }

            return (parsedSiteName, parsedSlotName);
        }

        /// <summary>
        /// Creates a new <see cref="ResourceIdentifier"/>, either for a <see cref="WebSiteResource"/> or a <see cref="WebSiteSlotResource"/>, depending on if <see cref="HasSlot"/> is <c>true</c>.
        /// </summary>
        /// <returns></returns>
        public ResourceIdentifier CreateResourceIdentifier()
        {
            return HasSlot
                ? WebSiteSlotResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName, Site, Slot)
                : CreateWebSiteResourceIdentifier();
        }
        /// <summary>
        /// Creates a new <see cref="ResourceIdentifier"/> for the root <see cref="WebSiteSlotResource"/>, even if this is targeting a slot.
        /// </summary>
        /// <returns></returns>
        public ResourceIdentifier CreateWebSiteResourceIdentifier()
            => WebSiteResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName, Site);
    }
}
#nullable restore