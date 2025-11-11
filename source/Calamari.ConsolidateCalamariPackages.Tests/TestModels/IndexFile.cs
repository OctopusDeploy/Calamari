using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Calamari.ConsolidateCalamariPackages.Tests.TestModels
{
    public class IndexFile
    {
        [Required]
        public Dictionary<string, PackageDetails> Packages { get; set; }
    }
}