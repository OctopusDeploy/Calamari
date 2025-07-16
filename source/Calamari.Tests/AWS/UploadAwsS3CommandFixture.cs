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
using System.Threading;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Serialization;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using SharpCompress.Archives.Zip;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class UploadAwsS3FixtureForExistingBucket : UploadAwsS3CommandFixture
    {
        // S3 Bucket operations are only eventually consistent (https://docs.aws.amazon.com/AmazonS3/latest/userguide/Welcome.html#ConsistencyModel),
        // For this fixture, we pre-create the bucket to avoid any timing issues where we get told "Bucket does not exist" when trying to validate
        // what we uploaded. Bucket creation is tested in S3FixtureForNewBucket.
        [OneTimeSetUp]
        public Task SetUpInfrastructure()
        {
            return Validate(async client =>
                            {
                                await client.PutBucketAsync(bucketName);
                                await client.PutBucketTaggingAsync(bucketName,
                                                                   new List<Tag>
                                                                   {
                                                                       new Tag { Key = "VantaOwner", Value = "modern-deployments-team@octopus.com" },
                                                                       new Tag { Key = "VantaNonProd", Value = "true" },
                                                                       new Tag { Key = "VantaNoAlert", Value = "Ephemeral bucket created during unit tests and not used in production" },
                                                                       new Tag { Key = "VantaContainsUserData", Value = "false" },
                                                                       new Tag { Key = "VantaUserDataStored", Value = "N/A" },
                                                                       new Tag { Key = "VantaDescription", Value = "Ephemeral bucket created during unit tests" }
                                                                   });
                            });
        }

        [Test]
        public async Task UploadPackage1()
        {
            TestContext.WriteLine("Region: " + region);

            var fileSelections = new List<S3TargetPropertiesBase>
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

            var prefix = await Upload("Package1", fileSelections);

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
            var fileSelections = new List<S3TargetPropertiesBase>
            {
                new S3MultiFileSelectionProperties
                {
                    Pattern = "**/Things/*",
                    Type = S3FileSelectionTypes.MultipleFiles,
                    StorageClass = "STANDARD",
                    CannedAcl = "private"
                }
            };

            var prefix = await Upload("Package2", fileSelections);

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
            var fileSelections = new List<S3TargetPropertiesBase>
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

            var prefix = await Upload("Package3", fileSelections, variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}file.json");
                               var text = new StreamReader(file.ResponseStream).ReadToEnd();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                           });
        }

        [Test]
        public async Task UploadPackage3Complete()
        {
            var packageOptions = new List<S3TargetPropertiesBase>
            {
                new S3PackageOptions()
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var prefix = await Upload("Package3", packageOptions, variables, S3TargetMode.EntirePackage);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}Package3.1.0.0.zip");
                               var memoryStream = new MemoryStream();
                               await file.ResponseStream.CopyToAsync(memoryStream);
                               var text = await new StreamReader(ZipArchive.Open(memoryStream).Entries.First(entry => entry.Key == "file.json").OpenEntryStream()).ReadToEndAsync();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                           });
        }

        [Test]
        public async Task UploadPackageWithContentHashAppended()
        {
            var packageOptions = new List<S3TargetPropertiesBase>
            {
                new S3PackageOptions()
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.FilenameWithContentHash,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            await Upload("Package3", packageOptions, variables, S3TargetMode.EntirePackage);
        }

        [Test]
        public async Task UploadPackage3Individual()
        {
            var fileSelections = new List<S3TargetPropertiesBase>
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

            await Upload("Package3", fileSelections, variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"myfile.json");
                               var text = new StreamReader(file.ResponseStream).ReadToEnd();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                           });
        }

        IDictionary<string, string> specialHeaders = new Dictionary<string, string>()
        {
            { "Cache-Control", "max-age=123" },
            { "Content-Disposition", "some disposition" },
            { "Content-Encoding", "some-encoding" },
            { "Content-Type", "application/html" },
            { "Expires", DateTime.UtcNow.AddDays(1).ToString("r") }, // Need to use RFC1123 format to match how the request is serialized
            { "x-amz-website-redirect-location", "/anotherPage.html" },
        };

        IDictionary<string, string> userDefinedMetadata = new Dictionary<string, string>()
        {
            { "Expect", "some-expect" },
            { "Content-MD5", "somemd5" },
            { "Content-Length", "12345" },
            { "x-amz-tagging", "sometag" },
            { "x-amz-storage-class", "GLACIER" },
            { "x-amz-meta", "somemeta" }
        };

        [Test]
        public async Task UploadPackageWithMetadata()
        {
            var fileSelections = new List<S3TargetPropertiesBase>
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

            var prefix = await Upload("Package1", fileSelections);

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
                                       .Should()
                                       .Be(userMetadata.Value);

                               response.TagCount.Should().Be(1);
                           });
        }

        [Test]
        [TestCase("TestZipPackage", "1.0.0", "zip")]
        [TestCase("TestJarPackage", "0.0.1-beta", "jar")]
        public async Task SubstituteVariablesAndUploadZipArchives(string packageId, string packageVersion, string packageExtension)
        {
            var fileName = $"{packageId}.{packageVersion}.{packageExtension}";

            var packageOptions = new List<S3PackageOptions>
            {
                new S3PackageOptions
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "CompressedPackages", fileName);

            var prefix = await UploadEntireCompressedPackage(packageFilePath,
                                                       packageId,
                                                       packageVersion,
                                                       packageOptions,
                                                       variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}{fileName}");
                               var memoryStream = new MemoryStream();
                               await file.ResponseStream.CopyToAsync(memoryStream);
                               var text = await new StreamReader(ZipArchive.Open(memoryStream).Entries.First(entry => entry.Key == "file.json").OpenEntryStream()).ReadToEndAsync();
                               JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                               memoryStream.Close();
                           });
        }

        [Test]
        public async Task SubstituteVariablesAndUploadTarArchives()
        {
            const string packageId = "TestTarPackage";
            const string packageVersion = "0.0.1";
            const string packageExtension = "tar";
            var fileName = $"{packageId}.{packageVersion}.{packageExtension}";

            var packageOptions = new List<S3PackageOptions>
            {
                new S3PackageOptions
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "CompressedPackages", fileName);

            var prefix = await UploadEntireCompressedPackage(packageFilePath,
                                                       packageId,
                                                       packageVersion,
                                                       packageOptions,
                                                       variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}{fileName}");
                               var tempFileName = Path.GetTempFileName();
                               var tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(tempFileName));
                               try
                               {
                                   using (var fs = File.OpenWrite(tempFileName))
                                   {
                                       await file.ResponseStream.CopyToAsync(fs);
                                   }

                                   var log = new InMemoryLog();
                                   var extractor = new TarPackageExtractor(log);
                                   extractor.Extract(tempFileName, tempDirectoryName);
                                   var text = File.ReadAllText(Path.Combine(tempDirectoryName, "file.json"));
                                   JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                               }
                               finally
                               {
                                   File.Delete(tempFileName);
                                   Directory.Delete(tempDirectoryName, true);
                               }
                           });
        }

        [Test]
        public async Task SubstituteVariablesAndUploadTarGzipArchives()
        {
            const string packageId = "TestTarGzipPackage";
            const string packageVersion = "0.0.1";
            const string packageExtension = "tar.gz";
            var fileName = $"{packageId}.{packageVersion}.{packageExtension}";

            var packageOptions = new List<S3PackageOptions>
            {
                new S3PackageOptions
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "CompressedPackages", fileName);

            var prefix = await UploadEntireCompressedPackage(packageFilePath,
                                                       packageId,
                                                       packageVersion,
                                                       packageOptions,
                                                       variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}{fileName}");
                               var tempFileName = Path.GetTempFileName();
                               var tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(tempFileName));
                               try
                               {
                                   using (var fs = File.OpenWrite(tempFileName))
                                   {
                                       await file.ResponseStream.CopyToAsync(fs);
                                   }

                                   var log = new InMemoryLog();
                                   var extractor = new TarGzipPackageExtractor(log);
                                   extractor.Extract(tempFileName, tempDirectoryName);
                                   var text = File.ReadAllText(Path.Combine(tempDirectoryName, "file.json"));
                                   JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                               }
                               finally
                               {
                                   File.Delete(tempFileName);
                                   Directory.Delete(tempDirectoryName, true);
                               }
                           });
        }

        [Test]
        public async Task SubstituteVariablesAndUploadTarBZip2Archives()
        {
            const string packageId = "TestTarBzip2Package";
            const string packageVersion = "0.0.1";
            const string packageExtension = "tar.bz2";
            var fileName = $"{packageId}.{packageVersion}.{packageExtension}";

            var packageOptions = new List<S3PackageOptions>
            {
                new S3PackageOptions
                {
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    StructuredVariableSubstitutionPatterns = "*.json",
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename,
                }
            };

            var variables = new CalamariVariables();
            variables.Set("Property1:Property2:Value", "InjectedValue");

            var packageFilePath = TestEnvironment.GetTestPath("AWS", "S3", "CompressedPackages", fileName);

            var prefix = await UploadEntireCompressedPackage(packageFilePath,
                                                       packageId,
                                                       packageVersion,
                                                       packageOptions,
                                                       variables);

            await Validate(async client =>
                           {
                               var file = await client.GetObjectAsync(bucketName, $"{prefix}{fileName}");
                               var tempFileName = Path.GetTempFileName();
                               var tempDirectoryName = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(tempFileName));
                               try
                               {
                                   using (var fs = File.OpenWrite(tempFileName))
                                   {
                                       await file.ResponseStream.CopyToAsync(fs);
                                   }

                                   var log = new InMemoryLog();
                                   var extractor = new TarBzipPackageExtractor(log);
                                   extractor.Extract(tempFileName, tempDirectoryName);
                                   var text = File.ReadAllText(Path.Combine(tempDirectoryName, "file.json"));
                                   JObject.Parse(text)["Property1"]["Property2"]["Value"].ToString().Should().Be("InjectedValue");
                               }
                               finally
                               {
                                   File.Delete(tempFileName);
                                   Directory.Delete(tempDirectoryName, true);
                               }
                           });
        }
    }

    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class UploadAwsS3FixtureForNewBucket : UploadAwsS3CommandFixture
    {
        [Test]
        public async Task UploadPackage1()
        {
            TestContext.WriteLine("Region: " + region);

            var fileSelections = new List<S3TargetPropertiesBase>
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

            var prefix = await Upload("Package1", fileSelections);

            await DoSafelyWithRetries(async () =>
                                      {
                                          await Validate(async client =>
                                                         {
                                                             await client.GetObjectAsync(bucketName, $"{prefix}Resources/TextFile.txt");
                                                             await client.GetObjectAsync(bucketName, $"{prefix}root/Page.html");
                                                             await client.GetObjectAsync(bucketName, $"{prefix}Extra/JavaScript.js");
                                                         });
                                      },
                                      5);
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

    public abstract class UploadAwsS3CommandFixture
    {
        protected string region;
        protected string bucketName;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        public UploadAwsS3CommandFixture()
        {
            region = RegionRandomiser.GetARegion();
            bucketName = $"calamari-s3fixture-{Guid.NewGuid():N}";
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
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken),
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));

            var config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };

            using (var client = new AmazonS3Client(credentials, config))
            {
                await execute(client);
            }
        }

        protected async Task<string> Upload(string packageName, List<S3TargetPropertiesBase> propertiesList, VariableDictionary customVariables = null, S3TargetMode s3TargetMode = S3TargetMode.FileSelections)
        {
            const string packageVersion = "1.0.0";
            var bucketKeyPrefix = $"calamaritest/{Guid.NewGuid():N}/";
            var variables = new CalamariVariables();

            propertiesList.ForEach(properties =>
                                   {
                                       switch (properties)
                                       {
                                           case S3MultiFileSelectionProperties multiFileSelectionProperties:
                                               multiFileSelectionProperties.BucketKeyPrefix = bucketKeyPrefix;
                                               variables.Set(AwsSpecialVariables.S3.FileSelections, JsonConvert.SerializeObject(propertiesList, GetEnrichedSerializerSettings()));
                                               break;
                                           case S3SingleFileSelectionProperties singleFileSelectionProperties:
                                               singleFileSelectionProperties.BucketKeyPrefix = bucketKeyPrefix;
                                               variables.Set(AwsSpecialVariables.S3.FileSelections, JsonConvert.SerializeObject(propertiesList, GetEnrichedSerializerSettings()));
                                               break;
                                           case S3PackageOptions packageOptions:
                                               packageOptions.BucketKeyPrefix = bucketKeyPrefix;
                                               variables.Set(AwsSpecialVariables.S3.PackageOptions, JsonConvert.SerializeObject(packageOptions, GetEnrichedSerializerSettings()));
                                               variables.Set(PackageVariables.PackageId, packageName);
                                               variables.Set(PackageVariables.PackageVersion, packageVersion);
                                               break;
                                       }
                                   });

            var variablesFile = Path.GetTempFileName();

            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));
            variables.Set("Octopus.Action.Aws.Region", region);

            if (customVariables != null) variables.Merge(customVariables);

            variables.Save(variablesFile);

            var packageDirectory = TestEnvironment.GetTestPath("AWS", "S3", packageName);
            using (var package = new TemporaryFile(PackageBuilder.BuildSimpleZip(packageName, packageVersion, packageDirectory)))
            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

                var command = new UploadAwsS3Command(
                                                     log,
                                                     variables,
                                                     fileSystem,
                                                     new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                                                     new ExtractPackage(new CombinedPackageExtractor(log, fileSystem, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log),
                                                     new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                                                                                          {
                                                                                              new JsonFormatVariableReplacer(fileSystem, log),
                                                                                              new XmlFormatVariableReplacer(fileSystem, log),
                                                                                              new YamlFormatVariableReplacer(fileSystem, log),
                                                                                              new PropertiesFormatVariableReplacer(fileSystem, log),
                                                                                          },
                                                                                          variables,
                                                                                          fileSystem,
                                                                                          log)
                                                    );

                var result = command.Execute(new[]
                {
                    "--package", $"{package.FilePath}",
                    "--variables", $"{variablesFile}",
                    "--bucket", bucketName,
                    "--targetMode", s3TargetMode.ToString()
                });

                result.Should().Be(0);
            }

            return bucketKeyPrefix;
        }

        protected async Task<string> UploadEntireCompressedPackage(string packageFilePath,
                                                       string packageId,
                                                       string packageVersion,
                                                       List<S3PackageOptions> propertiesList,
                                                       VariableDictionary customVariables = null)
        {
            var bucketKeyPrefix = $"calamaritest/{Guid.NewGuid():N}/";
            var variables = new CalamariVariables();

            propertiesList.ForEach(properties =>
                                   {
                                       properties.BucketKeyPrefix = bucketKeyPrefix;
                                       variables.Set(AwsSpecialVariables.S3.PackageOptions, JsonConvert.SerializeObject(properties, GetEnrichedSerializerSettings()));
                                       variables.Set(PackageVariables.PackageId, packageId);
                                       variables.Set(PackageVariables.PackageVersion, packageVersion);
                                   });

            var variablesFile = Path.GetTempFileName();

            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));
            variables.Set("Octopus.Action.Aws.Region", region);

            if (customVariables != null) variables.Merge(customVariables);

            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

                var command = new UploadAwsS3Command(
                                                     log,
                                                     variables,
                                                     fileSystem,
                                                     new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                                                     new ExtractPackage(new CombinedPackageExtractor(log, fileSystem, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log),
                                                     new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                                                                                          {
                                                                                              new JsonFormatVariableReplacer(fileSystem, log),
                                                                                              new XmlFormatVariableReplacer(fileSystem, log),
                                                                                              new YamlFormatVariableReplacer(fileSystem, log),
                                                                                              new PropertiesFormatVariableReplacer(fileSystem, log),
                                                                                          },
                                                                                          variables,
                                                                                          fileSystem,
                                                                                          log)
                                                    );

                var result = command.Execute(new[]
                {
                    "--package", $"{packageFilePath}",
                    "--variables", $"{variablesFile}",
                    "--bucket", bucketName,
                    "--targetMode", S3TargetMode.EntirePackage.ToString()
                });

                result.Should().Be(0);
            }

            return bucketKeyPrefix;
        }
    }
}
