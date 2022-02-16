#if AWS
using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using System.Threading.Tasks;
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
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;
using Calamari.Testing;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class S3FixtureForExistingBucket : S3Fixture
    {
        // S3 Bucket operations are only eventually consistent (https://docs.aws.amazon.com/AmazonS3/latest/userguide/Welcome.html#ConsistencyModel),
        // For this fixture, we pre-create the bucket to avoid any timing issues where we get told "Bucket does not exist" when trying to validate
        // what we uploaded. Bucket creation is tested in S3FixtureForNewBucket.
        [OneTimeSetUp]
        public Task SetUpInfrastructure()
        {
            return Validate(async client => await client.PutBucketAsync(bucketName));
        }

        [Test]
        public async Task UploadPackage1()
        {
            TestContext.WriteLine("Region: " + region);
            
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

            await Validate(async client =>
            {
                await client.GetObjectAsync(bucketName, $"{prefix}Resources/TextFile.txt");
                await client.GetObjectAsync(bucketName, $"{prefix}root/Page.html");
                await client.GetObjectAsync(bucketName, $"{prefix}Extra/JavaScript.js");
            });
        }

        [Test]
        public async Task UploadPackage2()
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

            await Validate(async client =>
            {
                await client.GetObjectAsync(bucketName, $"{prefix}Wild/Things/TextFile2.txt");
                try
                {
                    await client.GetObjectAsync(bucketName, $"{prefix}Wild/Ignore/TextFile1.txt");
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

        [Test]
        public async Task UploadPackage3()
        {
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3MultiFileSelectionProperties
                {
                    Pattern = "*.json",
                    Type = S3FileSelectionTypes.MultipleFiles,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json"
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var prefix = Upload("Package3", fileSelections, variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}file.json");
                               var text = new StreamReader(file.ResponseStream).ReadToEnd();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                           });
        }

        [Test]
        public async Task UploadPackage3Individual()
        {
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3SingleFileSelectionProperties
                {
                    Path = "file.json",
                    Type = S3FileSelectionTypes.SingleFile,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Custom,
                    BucketKey = "myfile.json",
                    PerformStructuredVariableSubstitution = true
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            Upload("Package3", fileSelections, variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"myfile.json");
                               var text = new StreamReader(file.ResponseStream).ReadToEnd();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                           });
        }

        IDictionary<string, string> specialHeaders = new Dictionary<string, string>()
        {
            {"Cache-Control", "max-age=123"},
            {"Content-Disposition", "some disposition"},
            {"Content-Encoding", "some-encoding"},
            {"Content-Type", "application/html"},
            {"Expires", DateTime.UtcNow.AddDays(1).ToString("r")}, // Need to use RFC1123 format to match how the request is serialized
            {"x-amz-website-redirect-location", "/anotherPage.html"},
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
        public async Task UploadPackageWithMetadata()
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

            await Validate(async client =>
            {
                var response = await client.GetObjectAsync(bucketName, $"{prefix}Extra/JavaScript.js");
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
    }

    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class S3FixtureForNewBucket : S3Fixture
    {
        [Test]
        public async Task UploadPackage1()
        {
            TestContext.WriteLine("Region: " + region);
            
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

            await DoSafelyWithRetries(async() => {
                await Validate(async client =>
                {
                    await client.GetObjectAsync(bucketName, $"{prefix}Resources/TextFile.txt");
                    await client.GetObjectAsync(bucketName, $"{prefix}root/Page.html");
                    await client.GetObjectAsync(bucketName, $"{prefix}Extra/JavaScript.js");
                });
            }, 5);
            
        }

        async Task DoSafelyWithRetries(Func<Task> action, int maxRetries)
        {
            for (int retry = 1; retry <= maxRetries; retry++)
            {
                try
                {
                    await action();
                    TestContext.WriteLine($"Validate succeeded on retry {retry}");
                    break;
                }
                catch (Exception e)
                {
                    TestContext.WriteLine($"Validate failed on retry {retry}: {e.Message}");
                    if (retry == maxRetries) throw;
                    await Task.Delay(500);
                }
            }
        }
    }

    public abstract class S3Fixture
    {
        protected string region;
        protected string bucketName;

        public S3Fixture()
        {
            region = RegionRandomiser.GetARegion();
            bucketName = Guid.NewGuid().ToString("N");
        }

        [OneTimeTearDown]
        public Task TearDownInfrastructure()
        {
            return Validate(async client =>
                            {
                                var response = await client.ListObjectsAsync(bucketName);
                                foreach (var s3Object in response.S3Objects)
                                {
                                    await client.DeleteObjectAsync(bucketName, s3Object.Key);
                                }
                                await client.DeleteBucketAsync(bucketName);
                            });
        }

        protected static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x =>
                {
                    x.Converters.Add(new FileSelectionsConverter());
                    x.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
        }

        protected async Task Validate(Func<AmazonS3Client, Task> execute)
        {
            var credentials = new BasicAWSCredentials(
                ExternalVariables.Get(ExternalVariable.AwsAcessKey),
                ExternalVariables.Get(ExternalVariable.AwsSecretKey));

            var config = new AmazonS3Config {AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region)};
            
            using (var client = new AmazonS3Client(credentials, config))
            {
                await execute(client);
            }
        }

        protected string Upload(string packageName, List<S3FileSelectionProperties> fileSelections, VariableDictionary customVariables = null)
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
            variables.Set("AWSAccount.AccessKey", ExternalVariables.Get(ExternalVariable.AwsAcessKey));
            variables.Set("AWSAccount.SecretKey", ExternalVariables.Get(ExternalVariable.AwsSecretKey));
            variables.Set("Octopus.Action.Aws.Region", region);
            variables.Set(AwsSpecialVariables.S3.FileSelections, JsonConvert.SerializeObject(fileSelections, GetEnrichedSerializerSettings()));

            if (customVariables != null) variables.Merge(customVariables);
            
            variables.Save(variablesFile);

            var packageDirectory = TestEnvironment.GetTestPath("AWS", "S3", packageName);
            using (var package = new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, "1.0.0", packageDirectory)))
            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

                var command = new UploadAwsS3Command(
                    log,
                    variables,
                    fileSystem,
                    new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                    new ExtractPackage(new CombinedPackageExtractor(log, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log),
                    new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                    {
                        new JsonFormatVariableReplacer(fileSystem, log),
                        new XmlFormatVariableReplacer(fileSystem, log),
                        new YamlFormatVariableReplacer(fileSystem, log),
                        new PropertiesFormatVariableReplacer(fileSystem, log),
                    }, variables, fileSystem, log)
                );

                var result = command.Execute(new[] {
                    "--package", $"{package.FilePath}",
                    "--variables", $"{variablesFile}",
                    "--bucket", bucketName,
                    "--targetMode", S3TargetMode.FileSelections.ToString()});

                result.Should().Be(0);
            }

            return bucketKeyPrefix;
        }
    }
}
#endif