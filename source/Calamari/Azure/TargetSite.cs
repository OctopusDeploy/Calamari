﻿namespace Calamari.Azure
 {
    class TargetSite
    {
        public string RawSite { get; set; }
        public string Site { get; set; }
        public string Slot { get; set; }

        public string SiteAndSlot => HasSlot ? $"{Site}/{Slot}" : Site;
        public string ScmSiteAndSlot => HasSlot ? $"{Site}-{Slot}" : Site;
        
        public bool HasSlot => !string.IsNullOrWhiteSpace(Slot);

        public TargetSite()
        {
            RawSite = "";
            Site = "";
        }
    }
}