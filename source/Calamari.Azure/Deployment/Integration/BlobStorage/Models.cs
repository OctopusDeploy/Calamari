using System.Collections.Generic;

namespace Calamari.Azure.Deployment.Integration.BlobStorage
{
    public enum TargetMode {
        EntirePackage,
        FileSelections
    }

    public class FileSelectionProperties
    {
        public FileSelectionProperties()
        {
            Metadata = new Dictionary<string, string>();
        }

        public string Pattern { get; set; }
        public bool FailIfNoMatches { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}
