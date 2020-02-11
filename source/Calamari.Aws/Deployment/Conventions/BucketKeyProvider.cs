using System;
using Calamari.Aws.Integration.S3;

namespace Calamari.Aws.Deployment.Conventions
{
    public interface IBucketKeyProvider
    {
        string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour);
    }
    
    public class BucketKeyProvider : IBucketKeyProvider
    {
        public string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour)
        {
            switch (behaviour.BucketKeyBehaviour)
            {
                case BucketKeyBehaviourType.Custom:
                    return behaviour.BucketKey;
                case BucketKeyBehaviourType.Filename:
                    return $"{behaviour.BucketKeyPrefix}{defaultKey}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(behaviour), "The provided bucket key behavior was not valid.");
            }
        }
    }
}