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
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
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

        static void Validate(Action<AmazonS3Client> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_Calamari_Access"), Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonS3Config
            {
                AllowAutoRedirect = true,
                RegionEndpoint = RegionEndpoint.APSoutheast1
                
            };
            using (var client = new AmazonS3Client(credentials, config))
            {
                execute(client);
            }
        }

        string Upload(string packageName, List<S3FileSelectionProperties> fileSelections)
        {
            var bucketKeyPrefix = $"test/{Guid.NewGuid():N}/";

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
            var variables = new VariableDictionary();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Set(AwsSpecialVariables.S3.FileSelections, JsonConvert.SerializeObject(fileSelections, GetEnrichedSerializerSettings()));
            variables.Save(variablesFile);

            var packageDirectory = TestEnvironment.GetTestPath("AWS", "S3", packageName);
            using (var package = new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, "1.0.0", packageDirectory)))
            using (new TemporaryFile(variablesFile))
            {
                var command = new UploadAwsS3Command();
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
