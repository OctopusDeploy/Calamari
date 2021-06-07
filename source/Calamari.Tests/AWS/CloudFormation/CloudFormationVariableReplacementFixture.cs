#if AWS
using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon.S3;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture, Explicit]
    public class CloudFormationVariableReplacementFixture
    {
        private const string StackName = "octopuse2ecftests";
        private const string ReplacedName = "octopuse2e-replaced";

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task CreateCloudFormationWithStructuredVariableReplacement()
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
            try
            {
                DeployTemplate(StackName, template);

                await ValidateCloudFormation(async (client) =>
                {
                    Func<IAmazonCloudFormation> clientFactory = () => client;
                    var stackStatus = await clientFactory.StackExistsAsync(new StackArn(StackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                    stackStatus.Should().Be(Aws.Deployment.Conventions.StackStatus.Completed);
                });

                ValidateS3(client =>
                {
                    // Bucket with replaced name was created successfully
                    client.GetBucketLocation(ReplacedName);
                });
            }
            finally
            {
                try
                {
                    DeleteStack(StackName);
                }
                catch (Exception e)
                {
                    Log.Error($"Error occurred while attempting to delete stack {StackName} -> {e}." + 
                              $"{Environment.NewLine} Test resources may not have been deleted, please check the AWS console for the status of the stack.");
                }
            }
        }

        void ValidateS3(Action<AmazonS3Client> execute)
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
            var templateFilePath = Path.GetTempFileName();
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Set(KnownVariables.Package.EnabledFeatures, "Octopus.Features.JsonConfigurationVariables");
            variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, templateFilePath);
            variables.Set($"Resources:{StackName}:Properties:BucketName", ReplacedName);
            variables.Save(variablesFile);

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
                var result = command.Execute(new[]
                {
                    "--template", $"{templateFile.FilePath}",
                    "--variables", $"{variablesFile}",
                    "--stackName", resourceName,
                    "--waitForCompletion", "true"
                });

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
                var command = new DeleteCloudFormationCommand(log, variables);
                var result = command.Execute(new[]
                {
                    "--variables", $"{variablesFile}",
                    "--waitForCompletion", "true"
                });

                result.Should().Be(0);
            }
        }
    }
}
#endif