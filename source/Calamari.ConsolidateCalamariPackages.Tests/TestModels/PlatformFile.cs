using System.ComponentModel.DataAnnotations;

namespace Calamari.ConsolidateCalamariPackages.Tests.TestModels
{
    public class PlatformFile
    {
        string source;
        string destination;

        [Required]
        public string Source
        {
            get => source;
            set => source = value.SanitiseHash().Sanitise4PartVersions();
        }

        [Required]
        public string Destination
        {
            get => destination;
            set => destination = value.SanitiseHash().Sanitise4PartVersions();
        }
    }
}