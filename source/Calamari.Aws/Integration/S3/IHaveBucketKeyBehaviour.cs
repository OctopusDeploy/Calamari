namespace Calamari.Aws.Integration.S3
{
    public interface IHaveBucketKeyBehaviour
    {
        BucketKeyBehaviourType BucketKeyBehaviour { get; }
        string BucketKey { get; }
        string BucketKeyPrefix { get; }
    }

    public class S3MultiFileSelecitonBucketKeyAdapter: IHaveBucketKeyBehaviour
    {
        private readonly S3MultiFileSelectionProperties properties;

        public S3MultiFileSelecitonBucketKeyAdapter(S3MultiFileSelectionProperties properties)
        {
            this.properties = properties;
        }

        public BucketKeyBehaviourType BucketKeyBehaviour => BucketKeyBehaviourType.Filename;
        public string BucketKey => BucketKeyPrefix;
        public string BucketKeyPrefix => properties.BucketKeyPrefix;
    }
}