using System.Collections.Generic;

namespace Sashimi.Aws.Validation
{
    public class S3PackageProperties
    {
        public List<KeyValuePair<string, string>> Tags { get; } = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> Metadata { get; } = new List<KeyValuePair<string, string>>();
        public string CannedAcl { get; set; }
        public BucketKeyBehaviourType BucketKeyBehaviour { get; set; }
        public string BucketKeyPrefix { get; set; }
        public string BucketKey { get; set; }
        public string StorageClass { get; set; }
    }
}