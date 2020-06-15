namespace Calamari.Aws.Integration.S3
{
    public interface IHaveBucketKeyBehaviour
    {
        BucketKeyBehaviourType BucketKeyBehaviour { get; }
        string BucketKey { get; }
        string BucketKeyPrefix { get; }
    }
}