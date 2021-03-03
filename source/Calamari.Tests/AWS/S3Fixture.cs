using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
#if AWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Serialization;
using Calamari.Integration.FileSystem;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Tests.AWS
{
    [TestFixture, Explicit]
    public class S3Fixture
    {
        private const string BucketName = "octopus-e2e-tests";

        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x =>
                {
                    x.Converters.Add(new FileSelectionsConverter());
                    x.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void UploadPackage1()
        {
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3MultiFileSelectionProperties
                {
                    Pattern = "Content/**/*", 
                    Type = S3FileSelectionTypes.MultipleFiles,
                    StorageClass = "STANDARD",
                    CannedAcl = "private"
                },
                new S3SingleFileSelectionProperties
                {
                    Path = "Extra/JavaScript.js", 
                    Type = S3FileSelectionTypes.SingleFile,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename
                }
            };

            var prefix = Upload("Package1", fileSelections);

            Validate(client =>
            {
                client.GetObject(BucketName, $"{prefix}Resources/TextFile.txt");
                client.GetObject(BucketName, $"{prefix}root/Page.html");
                client.GetObject(BucketName, $"{prefix}JavaScript.js");
            });
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void UploadPackage2()
        {
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3MultiFileSelectionProperties
                {
                    Pattern = "**/Things/*", 
                    Type = S3FileSelectionTypes.MultipleFiles,
                    StorageClass = "STANDARD",
                    CannedAcl = "private"
                }
            };

            var prefix = Upload("Package2", fileSelections);

            Validate(client =>
            {
                client.GetObject(BucketName, $"{prefix}Wild/Things/TextFile2.txt");
                try
                {
                    client.GetObject(BucketName, $"{prefix}Wild/Ignore/TextFile1.txt");
                }
                catch (AmazonS3Exception e)
                {
                    if (e.StatusCode != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }
            });
        }

        IDictionary<string, string> specialHeaders = new Dictionary<string, string>()
        {
            {"Cache-Control", "max-age=123"},
            {"Content-Disposition", "some disposition"},
            {"Content-Encoding", "some-encoding"},
            {"Content-Type", "some-content"},
            {"Expires", "2020-01-02T00:00:00.000Z"},
            {"x-amz-website-redirect-location", "/anotherPage.html"},
            
            //Locking requires a bucket with versioning
//            {"x-amz-object-lock-mode", "GOVERNANCE"},
//            {"x-amz-object-lock-retain-until-date", DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffK")},
//            {"x-amz-object-lock-legal-hold", "OFF"},
        };

        IDictionary<string, string> userDefinedMetadata = new Dictionary<string, string>()
        {
            {"Expect", "some-expect"},
            {"Content-MD5", "somemd5"},
            {"Content-Length", "12345"},
            {"x-amz-tagging", "sometag"},
            {"x-amz-storage-class", "GLACIER"},
            {"x-amz-meta", "somemeta"}
        };

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void UploadPackageWithMetadata()
        {
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3SingleFileSelectionProperties
                {
                    Path = "Extra/JavaScript.js",
                    Type = S3FileSelectionTypes.SingleFile,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                    Metadata = specialHeaders.Concat(userDefinedMetadata).ToList(),
                    Tags = new List<KeyValuePair<string, string>>()
                    {
                        new KeyValuePair<string, string>("Environment", "Test")
                    }
                }
            };

            var prefix = Upload("Package1", fileSelections);

            Validate(client =>
            {
                var response = client.GetObject(BucketName, $"{prefix}JavaScript.js");
                var headers = response.Headers;
                var metadata = response.Metadata;

                foreach (var specialHeader in specialHeaders)
                {
                    if (specialHeader.Key == "Expires")
                    {
                        //There's a serialization bug in Json.Net that ends up changing the time to local.
                        //Fix this assertion once that's done.
                        var expectedDate = DateTime.Parse(specialHeader.Value.TrimEnd('Z')).ToUniversalTime();
                        response.Expires.Should().Be(expectedDate);
                    }
                    else if (specialHeader.Key == "x-amz-website-redirect-location")
                    {
                        response.WebsiteRedirectLocation.Should().Be(specialHeader.Value);
                    }
                    else
                        headers[specialHeader.Key].Should().Be(specialHeader.Value);
                }

                foreach (var userMetadata in userDefinedMetadata)
                    metadata["x-amz-meta-" + userMetadata.Key.ToLowerInvariant()]
                        .Should().Be(userMetadata.Value);

                response.TagCount.Should().Be(1);
            });
        }

        static void Validate(Action<AmazonS3Client> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_Calamari_Access"),
                Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonS3Config {AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.APSoutheast1};
            using (var client = new AmazonS3Client(credentials, config))
            {
                execute(client);
            }
        }

        string Upload(string packageName, List<S3FileSelectionProperties> fileSelections)
        {
            var bucketKeyPrefix = $"calamaritest/{Guid.NewGuid():N}/";

            fileSelections.ForEach(properties =>
            {
                if (properties is S3MultiFileSelectionProperties multiFileSelectionProperties)
                {
                    multiFileSelectionProperties.BucketKeyPrefix = bucketKeyPrefix;
                }

                if (properties is S3SingleFileSelectionProperties singleFileSelectionProperties)
                {
                    singleFileSelectionProperties.BucketKeyPrefix = bucketKeyPrefix;
                }
            });

            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Set(AwsSpecialVariables.S3.FileSelections,
                JsonConvert.SerializeObject(fileSelections, GetEnrichedSerializerSettings()));
            variables.Save(variablesFile);

            var packageDirectory = TestEnvironment.GetTestPath("AWS", "S3", packageName);
            using (var package =
                new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, "1.0.0", packageDirectory)))
            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                var command = new UploadAwsS3Command(
                    log,
                    variables,
                    fileSystem,
                    new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                    new ExtractPackage(new CombinedPackageExtractor(log, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log)
                );
                var result = command.Execute(new[] { 
                    "--package", $"{package.FilePath}", 
                    "--variables", $"{variablesFile}", 
                    "--bucket", BucketName, 
                    "--targetMode", S3TargetMode.FileSelections.ToString()});

                result.Should().Be(0);
            }

            return bucketKeyPrefix;
        }
    }
}
#endif