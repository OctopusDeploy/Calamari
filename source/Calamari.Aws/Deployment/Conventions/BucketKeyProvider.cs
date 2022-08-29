using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Aws.Integration.S3;
using Calamari.Common.Features.Packages;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Deployment.Conventions
{
    public interface IBucketKeyProvider
    {
        string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour, string packageFilePath = "");
        string EncodeBucketKeyForUrl(string bucketKey);
    }
    
    public class BucketKeyProvider : IBucketKeyProvider
    {
        public string GetBucketKey(string defaultKey, IHaveBucketKeyBehaviour behaviour, string packageFilePath = "")
        {
            var packageContentHash = string.Empty;
            if (!packageFilePath.IsNullOrEmpty())
            {
                packageContentHash = CalculateContentHash(packageFilePath);
            }
            switch (behaviour.BucketKeyBehaviour)
            {
                case BucketKeyBehaviourType.Custom:
                    return SubstitutePackageHashVariable(behaviour.BucketKey, packageContentHash);
                case BucketKeyBehaviourType.Filename:
                    return $"{SubstitutePackageHashVariable(behaviour.BucketKeyPrefix, packageContentHash)}{defaultKey}";
                case BucketKeyBehaviourType.FilenameWithContentHash:
                    var (fileName, extension) = GetFileNameParts(defaultKey);
                    var bucketKey = new StringBuilder();
                    bucketKey.Append(SubstitutePackageHashVariable(behaviour.BucketKeyPrefix, packageContentHash));
                    bucketKey.Append(fileName);
                    if (!packageContentHash.IsNullOrEmpty()) bucketKey.Append($"@{packageContentHash}");
                    bucketKey.Append(extension);
                    return bucketKey.ToString();
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

        string SubstitutePackageHashVariable(string input, string contentHash)
        {
            return input.Replace($"#{{Octopus.Action.Package.PackageContentHash}}", contentHash);
        }
        
        public string EncodeBucketKeyForUrl(string bucketKey)
        {
            var prefix = Path.GetDirectoryName(bucketKey)?.Replace('\\','/') ?? string.Empty;
            var (fileName, extension) = GetFileNameParts(Path.GetFileName(bucketKey));
            var encodedBucketKey = new StringBuilder();
            if (!prefix.IsNullOrEmpty()) encodedBucketKey.Append($"{prefix}/");
            encodedBucketKey.Append(Uri.EscapeDataString(fileName));
            encodedBucketKey.Append(extension);
            return encodedBucketKey.ToString();
        }

        (string filename, string extension) GetFileNameParts(string fileNameWithExtensions)
        {
            return PackageName.TryMatchTarExtensions(fileNameWithExtensions, out var fileName, out var extension) 
                ? (fileName, extension) 
                : (Path.GetFileNameWithoutExtension(fileNameWithExtensions), Path.GetExtension(fileNameWithExtensions));
        }
    }
}