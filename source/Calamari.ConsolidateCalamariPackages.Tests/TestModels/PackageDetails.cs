using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Calamari.ConsolidateCalamariPackages.Tests.TestModels
{
    public class PackageDetails
    {
        [Required]
        public string PackageId { get; set; }
        [Required]
        public string Version { get; set; }
        [Required]
        public bool IsNupkg { get; set; }
        [Required]
        public Dictionary<string, PlatformFile[]> PlatformFiles { get; set; }

    }
}