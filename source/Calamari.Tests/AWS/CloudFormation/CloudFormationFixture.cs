using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using System.Threading.Tasks;
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
using Calamari.Aws.Integration.CloudFormation;
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
    [TestFixture]
    public class CloudFormationFixture
    {
        private const string StackName = "octopuse2ecftests";

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task CreateOrUpdateCloudFormationTemplate()
        {
            var template = $@"
{{
    ""Resources"": {{
        ""{StackName}"": {{
            ""Type"": ""AWS::S3::Bucket"",
            ""Properties"": {{ 
                ""BucketName"": {StackName}
            }}
        }}
    }}
}}";
            DeployTemplate(StackName, template);

            await ValidateCloudFormation(async (client) =>
            {
                Func<IAmazonCloudFormation> clientFactory = () => client;
                var stackStatus = await clientFactory.StackExistsAsync(new StackArn(StackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                stackStatus.Should().Be(Aws.Deployment.Conventions.StackStatus.Completed);
            });

            Validate(client =>
            {
                // Bucket can be created successfully
                client.GetBucketLocation(StackName);
            });
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task DeleteCloudFormationStack()
        {
            var template = $@"
{{
    ""Resources"": {{
        ""{StackName}"": {{
            ""Type"": ""AWS::S3::Bucket"",
            ""Properties"": {{ 
                ""BucketName"": {StackName}
            }}
        }}
    }}
}}";
            DeployTemplate(StackName, template);
            DeleteStack(StackName);

            await ValidateCloudFormation(async (client) =>
            {
                Func<IAmazonCloudFormation> clientFactory = () => client;
                var stackStatus = await clientFactory.StackExistsAsync(new StackArn(StackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                stackStatus.Should().Be(Aws.Deployment.Conventions.StackStatus.DoesNotExist);
            });
        }

        void Validate(Action<AmazonS3Client> execute)
        {
            var credentials = new BasicAWSCredentials(
                Environment.GetEnvironmentVariable("AWS_Calamari_Access"),
                Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.APSoutheast1 };
            using (var client = new AmazonS3Client(credentials, config))
            {
                execute(client);
            }
        }

        async Task ValidateCloudFormation(Func<AmazonCloudFormationClient, Task> execute)
        {
            var credentials = new BasicAWSCredentials(
                Environment.GetEnvironmentVariable("AWS_Calamari_Access"),
                Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonCloudFormationConfig { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.APSoutheast1 };
            using (var client = new AmazonCloudFormationClient(credentials, config))
            {
                await execute(client);
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
                    "--stackName", resourceName,
                    "--waitForCompletion", "true"});

                result.Should().Be(0);
            }
        }

        void DeleteStack(string stackName)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Set(AwsSpecialVariables.CloudFormation.StackName, stackName);
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                var command = new DeleteCloudFormationCommand(
                    log,
                    variables
                );
                var result = command.Execute(new[] {
                    "--variables", $"{variablesFile}",
                    "--waitForCompletion", "true"});

                result.Should().Be(0);
            }
        }
    }
}
#endif