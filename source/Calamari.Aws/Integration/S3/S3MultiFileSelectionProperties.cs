using Newtonsoft.Json;

namespace Calamari.Aws.Integration.S3
{
    public class S3MultiFileSelectionProperties : S3FileSelectionProperties
    {
        public string BucketKeyPrefix { get; set; }
        public string Pattern { get; set; }

        public string VariableSubstitutionPatterns { get; set; }
        public string StructuredVariableSubstitutionPatterns { get; set; }
    }
}