using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.S3;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.S3
{
    [TestFixture]
    public class BucketKeyProviderFixture
    {
        IBucketKeyProvider sut;
        [SetUp]
        public void Setup()
        {
            sut = new BucketKeyProvider();
        }
        
        [Test]
        public void PackageFilenameBehaviorUsesDefaultWithPrefix()
        {
            var result = sut.GetBucketKey("default",
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
            var result = sut.GetBucketKey("default",
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
            var result = sut.GetBucketKey("default",
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
            var result = sut.GetBucketKey("default",
                new S3SingleFileSelectionProperties
                {
                    BucketKeyBehaviour = BucketKeyBehaviourType.Custom,
                    BucketKeyPrefix = "test/",
                    BucketKey = "something"
                });

            result.Should().Be("something");
        }

        [Test]
        public void EncodeBucketKeyForUrl_ShouldEncodeFileName()
        {
            var bucketKey = "dir/subdir/filename@ABC.extension";
            var result = BucketKeyProvider.EncodeBucketKeyForUrl(bucketKey);

            result.Should().Be("dir/subdir/filename%40ABC.extension");
        }
        
        [Test]
        public void EncodeBucketKeyForUrl_ShouldReturnInputStringIfNoEncodingRequired()
        {
            var bucketKey = "dir/subdir/filename.extension";
            var result = BucketKeyProvider.EncodeBucketKeyForUrl(bucketKey);

            result.Should().Be("dir/subdir/filename.extension");
        }
    }
}