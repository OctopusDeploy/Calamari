using System.Collections.Generic;

namespace Sashimi.Aws.Validation
{
    public class S3FileSelectionProperties
    {
        public string Type { get; set; }
        public List<KeyValuePair<string, string>> Tags { get; } = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> Metadata { get; } = new List<KeyValuePair<string, string>>();
        public string CannedAcl { get; set; }
        public BucketKeyBehaviourType BucketKeyBehaviour { get; set; }
        public string BucketKeyPrefix { get; set; }
        public string StorageClass { get; set; }

        //Multi File Properties

        public string Pattern { get; set; }
        public string VariableSubstitutionPatterns { get; set; }

        //Single File Properties
        public string Path { get; set; }
        public string BucketKey { get; set; }
        public string PerformVariableSubstitution { get; set; }
    }
}