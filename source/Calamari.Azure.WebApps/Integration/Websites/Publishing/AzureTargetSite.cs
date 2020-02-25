namespace Calamari.Azure.WebApps.Integration.Websites.Publishing
{
    public class AzureTargetSite
    {
        public string RawSite { get; set; }
        public string Site { get; set; }
        public string Slot { get; set; }

        public string SiteAndSlot => HasSlot ? $"{Site}/{Slot}" : Site;
        public string SiteAndSlotLegacy => HasSlot ? $"{Site}({Slot})" : Site;

        public bool HasSlot => !string.IsNullOrWhiteSpace(Slot);
    }
}