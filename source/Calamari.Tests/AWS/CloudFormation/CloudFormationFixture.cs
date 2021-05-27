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
    public class CloudFormationFixture
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
        public void CreateCloudFormationTemplate()
        {
            var resourceName = Guid.NewGuid().ToString("N"); // Only alphanumeric characters allowed in names of cloud formation resources
            var template = $@"
{{
    ""Resources"": {{
        ""{resourceName}"": {{
            ""Type"": ""AWS::S3::Bucket"",
            ""Properties"": {{ 
                ""BucketName"": {resourceName}
            }}
        }}
    }}
}}";
            DeployTemplate(resourceName, template);

            Validate(client =>
            {
                // Bucket can be created successfully
                client.GetBucketLocation(BucketName);
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

        void DeployTemplate(string resourceName, string template)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Save(variablesFile);

            var templateFilePath = Path.GetTempFileName();
            File.WriteAllText(templateFilePath, template);

            using (var templateFile = new TemporaryFile(templateFilePath))
            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                var command = new DeployCloudFormationCommand(
                    log,
                    variables,
                    fileSystem,
                    new ExtractPackage(new CombinedPackageExtractor(log, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log)
                );
                var result = command.Execute(new[] {
                    "--template", $"{templateFile.FilePath}",
                    "--variables", $"{variablesFile}",
                    "--stackName", resourceName});

                result.Should().Be(0);
            }
        }
    }
}
#endif