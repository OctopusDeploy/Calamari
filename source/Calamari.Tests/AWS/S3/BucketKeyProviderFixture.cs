using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.S3;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.S3
{
    [TestFixture]
    public class BucketKeyProviderFixture
    {
        [Test]
        public void PackageFilenameBehaviorUsesDefaultWithPrefix()
        {
            var provider = new BucketKeyProvider();
            var result = provider.GetBucketKey("default",
                new S3PackageOptions
                {
                    BucketKeyPrefix = "test/",
                    BucketKey = "something",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename
                });

            result.Should().Be("test/default");
        }
        
        [Test]
        public void PackageCustomBehaviorUsesProvidedBucketKey()
        {
            var provider = new BucketKeyProvider();
            var result = provider.GetBucketKey("default",
                new S3PackageOptions
                {
                    BucketKeyPrefix = "test/",
                    BucketKey = "something",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Custom
                });

            result.Should().Be("something");
        }

        [Test]
        public void SingleSelectionFilenameBehaviorUsesDefaultWithPrefix()
        {
            var provider = new BucketKeyProvider();
            var result = provider.GetBucketKey("default",
                new S3SingleFileSelectionProperties
                {
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                    BucketKeyPrefix = "test/",
                    BucketKey = "something"
                });

            result.Should().Be("test/default");
        }

        [Test]
        public void SingleFileSelectionCustomBehaviorUsesProvidedBucketKey()
        {
            var provider = new BucketKeyProvider();
            var result = provider.GetBucketKey("default",
                new S3SingleFileSelectionProperties
                {
                    BucketKeyBehaviour = BucketKeyBehaviourType.Custom,
                    BucketKeyPrefix = "test/",
                    BucketKey = "something"
                });

            result.Should().Be("something");
        }
    }
}