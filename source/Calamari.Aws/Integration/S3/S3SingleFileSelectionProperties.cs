namespace Calamari.Aws.Integration.S3
{

    public class S3SingleFileSelectionProperties : S3FileSelectionProperties, IHaveBucketKeyBehaviour
    {
        public string BucketKey { get; set; }
        public string BucketKeyPrefix { get; set; }
        public BucketKeyBehaviourType BucketKeyBehaviour { get; set; }
        public string Path { get; set; }
        public bool PerformVariableSubstitution { get; set; }
        public bool PerformStructuredVariableSubstitution { get; set; }
    }
}