namespace Calamari.Aws.Integration.S3
{
    public class S3PackageOptions : S3TargetPropertiesBase, IHaveBucketKeyBehaviour
    {
        public string BucketKey { get; set; }
        public string BucketKeyPrefix { get; set; }
        public BucketKeyBehaviourType BucketKeyBehaviour { get; set; }

        public string VariableSubstitutionPatterns { get; set; }
        public string StructuredVariableSubstitutionPatterns { get; set; }
    }
}