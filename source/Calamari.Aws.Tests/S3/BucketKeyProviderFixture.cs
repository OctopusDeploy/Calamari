using Calamari.Aws.Deployment.S3;
using Calamari.Aws.Integration.S3;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Aws.Tests.S3
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
    }
}