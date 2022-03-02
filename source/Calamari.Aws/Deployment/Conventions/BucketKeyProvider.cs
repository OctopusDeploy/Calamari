using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Aws.Integration.S3;
using Calamari.Common.Plumbing;

namespace Calamari.Aws.Deployment.Conventions
{
    public interface IBucketKeyProvider
    {
        string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour, string packageFilePath = "");
    }
    
    public class BucketKeyProvider : IBucketKeyProvider
    {
        public string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour, string packageFilePath = "")
        {
            if (behaviour.BucketKeyBehaviour == BucketKeyBehaviourType.FilenameWithContentHash)
            {
                
            }
            switch (behaviour.BucketKeyBehaviour)
            {
                case BucketKeyBehaviourType.Custom:
                    return behaviour.BucketKey;
                case BucketKeyBehaviourType.Filename:
                    return $"{behaviour.BucketKeyPrefix}{defaultKey}";
                case BucketKeyBehaviourType.FilenameWithContentHash:
                    Guard.NotNullOrWhiteSpace(packageFilePath, "BucketKeyBehaviourType.FilenameWithContentHash requires a package file path value");
                    var packageContentHash = CalculateContentHash(packageFilePath);
                    return $"@{packageContentHash}/{behaviour.BucketKeyPrefix}{defaultKey}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(behaviour), "The provided bucket key behavior was not valid.");
            }
        }

        string CalculateContentHash(string packageFilePath)
        {
            var packageContent = File.ReadAllBytes(packageFilePath);
            using (SHA256 sha256Hash = SHA256.Create())
            {
                var computedHashByte = sha256Hash.ComputeHash(packageContent);
                var computedHash = new StringBuilder();
                foreach (var c in computedHashByte)
                {
                    computedHash.Append(c.ToString("X2"));
                }

                return computedHash.ToString();
            }
        }
    }
}