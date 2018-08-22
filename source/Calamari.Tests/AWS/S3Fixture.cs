#if AWS
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Serialization;
using Calamari.Integration.FileSystem;
using Calamari.Serialization;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    public class S3Fixture
    {
        private static JsonSerializerSettings GetEnrichedSerializerSettings()
        {
            return JsonSerialization.GetDefaultSerializerSettings()
                .Tee(x =>
                {
                    x.Converters.Add(new FileSelectionsConverter());
                    x.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });
        }

        [Test, Explicit]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void Execute()
        {
            var bucketKeyPrefix = $"test/{Guid.NewGuid():N}/";
            var fileSelections = new List<S3FileSelectionProperties>
            {
                new S3MultiFileSelectionProperties
                {
                    Pattern = "Content/**/*", 
                    Type = S3FileSelectionTypes.MultipleFiles,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    BucketKeyPrefix = bucketKeyPrefix
                },
                new S3SingleFileSelectionProperties
                {
                    Path = "Extra/JavaScript.js", 
                    Type = S3FileSelectionTypes.SingleFile,
                    StorageClass = "STANDARD",
                    CannedAcl = "private",
                    BucketKeyPrefix = bucketKeyPrefix,
                    BucketKeyBehaviour = BucketKeyBehaviourType.Filename
                }
            };

            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", "ap-southeast-1");
            variables.Set(AwsSpecialVariables.S3.FileSelections, JsonConvert.SerializeObject(fileSelections, GetEnrichedSerializerSettings()));
            variables.Save(variablesFile);

            var packageDirectory = TestEnvironment.GetTestPath("AWS", "S3", "Package1");
            using (var package = new TemporaryFile(PackageBuilder.BuildSimpleZip("Package1", "1.0.0", packageDirectory)))
            using (new TemporaryFile(variablesFile))
            {
                var command = new UploadAwsS3Command();
                var result = command.Execute(new[] { 
                    "--package", $"{package.FilePath}", 
                    "--variables", $"{variablesFile}", 
                    "--bucket", "octopus-e2e-tests", 
                    "--targetMode", S3TargetMode.FileSelections.ToString()});
                
                Assert.AreEqual(0, result);
            }
        }
    }
}
#endif
