using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Amazon.CloudFormation;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
#if AWS
using System;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture, Explicit]
    public class CloudFormationFixture
    {
        private const string StackName = "octopuse2ecftests";

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task CreateOrUpdateCloudFormationTemplate()
        {
            var template = GetBasicS3Template(StackName);

            var templateFilePath = Path.GetTempFileName();
            var variables = new CalamariVariables();
            File.WriteAllText(templateFilePath, template);

            try
            {
                DeployTemplate(StackName, templateFilePath, variables);

                await ValidateStackExists(StackName, true);

                ValidateS3(client =>
                {
                    // Bucket can be created successfully
                    client.GetBucketLocation(StackName);
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
                    Log.Error($"Error occurred while attempting to delete stack {StackName} -> {e}." + $"{Environment.NewLine} Test resources may not have been deleted, please check the AWS console for the status of the stack.");
                }
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task DeleteCloudFormationStack()
        {
            var template = GetBasicS3Template(StackName);
            var templateFilePath = Path.GetTempFileName();
            var variables = new CalamariVariables();
            File.WriteAllText(templateFilePath, template);

            DeployTemplate(StackName, templateFilePath, variables);
            DeleteStack(StackName);
            await ValidateStackExists(StackName, false);
        }

        public static string GetBasicS3Template(string stackName)
        {
            return $@"
    {{
        ""Resources"": {{
            ""{stackName}"": {{
                ""Type"": ""AWS::S3::Bucket"",
                ""Properties"": {{ 
                    ""BucketName"": {stackName}
                }}
            }}
        }}
    }}";
        }

        public static void ValidateS3(Action<AmazonS3Client> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_Calamari_Access"), Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.APSoutheast1 };
            using (var client = new AmazonS3Client(credentials, config))
            {
                execute(client);
            }
        }

        async Task ValidateStackExists(string stackName, bool shouldExist)
        {
            await ValidateCloudFormation(async (client) =>
                                         {
                                             Func<IAmazonCloudFormation> clientFactory = () => client;
                                             var stackStatus = await clientFactory.StackExistsAsync(new StackArn(stackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                                             stackStatus.Should().Be(shouldExist ? Aws.Deployment.Conventions.StackStatus.Completed : Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                                         });
        }

        public static async Task ValidateCloudFormation(Func<AmazonCloudFormationClient, Task> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_Calamari_Access"), Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            var config = new AmazonCloudFormationConfig { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.APSoutheast1 };
            using (var client = new AmazonCloudFormationClient(credentials, config))
            {
                await execute(client);
            }
        }

        public static void DeployTemplate(string resourceName, string templateFilePath, IVariables variables)
        {
            var variablesFile = Path.GetTempFileName();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
            variables.Set("Octopus.Action.Aws.Region", RegionEndpoint.APSoutheast1.SystemName);
            variables.Save(variablesFile);

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

        public static void DeleteStack(string stackName)
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