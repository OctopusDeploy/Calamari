using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.S3;
using Calamari.Tests.Helpers;
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
            var result = sut.EncodeBucketKeyForUrl(bucketKey);

            result.Should().Be("dir/subdir/filename%40ABC.extension");
        }

        [Test]
        public void EncodeBucketKeyForUrl_ShouldReturnInputStringIfNoEncodingRequired()
        {
            var bucketKey = "dir/subdir/filename.extension";
            var result = sut.EncodeBucketKeyForUrl(bucketKey);

            result.Should().Be("dir/subdir/filename.extension");
        }
        
        [Test]
        public void EncodeBucketKeyForUrl_ShouldEncodedFileNameIfThereNoPrefixes()
        {
            var bucketKey = "filename@ABC.extension";
            var result = sut.EncodeBucketKeyForUrl(bucketKey);

            result.Should().Be("filename%40ABC.extension");
        }

        [Test]
        public void GetBucketKey_PackageOptions_ShouldAppendContentHash()
        {
            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "ZipPackages", "TestPackage.zip");
            var packageContentHash = CalculateContentHash(packageFilePath);
            var fileName = "defaultKey";
            var result = sut.GetBucketKey($"{fileName}.zip",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test/",
                                              BucketKey = "something",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash
                                          },
                                          packageFilePath );

            result.Should().Be($"test/{fileName}@{packageContentHash}.zip");
        }
        
        [Test]
        public void GetBucketKey_PackageOptions_ShouldNotAppendContentHash_WhenPackageFilePathNotFound()
        {
            var fileName = "defaultKey";
            var result = sut.GetBucketKey($"{fileName}.zip",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test/",
                                              BucketKey = "something",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash
                                          },
                                          String.Empty );

            result.Should().Be($"test/{fileName}.zip");
        }
        
        [Test]
        public void GetBucketKey_PackageOptions_ShouldAppendContentHash_WhenExtensionHasMultipleParts()
        {
            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "ZipPackages", "TestPackage.zip");
            var packageContentHash = CalculateContentHash(packageFilePath);
            var fileName = "defaultKey";
            var result = sut.GetBucketKey($"{fileName}.tar.gz",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test/",
                                              BucketKey = "something",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash
                                          },
                                          packageFilePath );

            result.Should().Be($"test/{fileName}@{packageContentHash}.tar.gz");
        }

        [Test]
        public void GetBucketKey_PackageOptions_ShouldAppendContentHash_WhenFileNameHasVersionNumbers()
        {
            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "ZipPackages", "TestPackage.zip");
            var packageContentHash = CalculateContentHash(packageFilePath);
            var fileName = "defaultKey.1.0.0";
            var result = sut.GetBucketKey($"{fileName}.tar.gz",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test/",
                                              BucketKey = "something",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash
                                          },
                                          packageFilePath );

            result.Should().Be($"test/{fileName}@{packageContentHash}.tar.gz");
        }
        
        [Test]
        public void GetBucketKey_PackageOptions_ShouldAppendContentHash_WhenFileNameHasVersionNumbersAndReleaseTag()
        {
            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "ZipPackages", "TestPackage.zip");
            var packageContentHash = CalculateContentHash(packageFilePath);
            var fileName = "defaultKey.1.0.0-beta";
            var result = sut.GetBucketKey($"{fileName}.tar.gz",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test/",
                                              BucketKey = "something",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash
                                          },
                                          packageFilePath );

            result.Should().Be($"test/{fileName}@{packageContentHash}.tar.gz");
        }
        
        [Test]
        public void GetBucketKey_PackageOptions_ShouldSubstituteContentHashVariable()
        {
            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "ZipPackages", "TestPackage.zip");
            var packageContentHash = CalculateContentHash(packageFilePath);
            var fileName = "defaultKey";
            var result = sut.GetBucketKey($"{fileName}.zip",
                                          new S3PackageOptions
                                          {
                                              BucketKeyPrefix = "test",
                                              BucketKey = "something/#{Octopus.Action.Package.PackageContentHash}/customFileName.zip",
                                              BucketKeyBehaviour = BucketKeyBehaviourType.Custom
                                          },
                                          packageFilePath );

            result.Should().Be($"something/{packageContentHash}/customFileName.zip");
        }

        static string CalculateContentHash(string packageFilePath)
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