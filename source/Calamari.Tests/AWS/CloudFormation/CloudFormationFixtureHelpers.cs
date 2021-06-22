﻿using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.Runtime;
using Amazon;
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

namespace Calamari.Tests.AWS.CloudFormation
{
    public static class CloudFormationFixtureHelpers
    {
        static string region;
        static CloudFormationFixtureHelpers()
        {
            region = RegionRandomiser.GetARegion();
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

        public static string WriteTemplateFile(string template)
        {
            var templateFilePath = Path.GetTempFileName();
            File.WriteAllText(templateFilePath, template);
            return templateFilePath;
        }

        public static async Task ValidateS3BucketExists(string stackName)
        {
            await ValidateS3(async client =>
            {
                await client.GetBucketLocationAsync(stackName);
            });
        }

        public static async Task ValidateS3(Func<AmazonS3Client, Task> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Access"), Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Secret"));
            var config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
            using (var client = new AmazonS3Client(credentials, config))
            {
                await execute(client);
            }
        }

        public static async Task ValidateStackExists(string stackName, bool shouldExist)
        {
            await ValidateCloudFormation(async (client) =>
            {
                Func<IAmazonCloudFormation> clientFactory = () => client;
                var stackStatus = await clientFactory.StackExistsAsync(new StackArn(stackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                stackStatus.Should().Be(shouldExist ? Aws.Deployment.Conventions.StackStatus.Completed : Aws.Deployment.Conventions.StackStatus.DoesNotExist);
            });
        }

        static async Task ValidateCloudFormation(Func<AmazonCloudFormationClient, Task> execute)
        {
            var credentials = new BasicAWSCredentials(Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Access"), Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Secret"));
            var config = new AmazonCloudFormationConfig { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
            using (var client = new AmazonCloudFormationClient(credentials, config))
            {
                await execute(client);
            }
        }

        public static void DeployTemplate(string resourceName, string templateFilePath, IVariables variables)
        {
            var variablesFile = Path.GetTempFileName();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Secret"));
            variables.Set("Octopus.Action.Aws.Region", region);
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

        public static void CleanupStack(string stackName)
        {
            try
            {
                DeleteStack(stackName);
            }
            catch (Exception e)
            {
                Log.Error($"Error occurred while attempting to delete stack {stackName} -> {e}." +
                          $"{Environment.NewLine} Test resources may not have been deleted, please check the AWS console for the status of the stack.");
            }
        }

        public static void DeleteStack(string stackName)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Access"));
            variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_OctopusAPITester_Secret"));
            variables.Set("Octopus.Action.Aws.Region", region);
            variables.Set(AwsSpecialVariables.CloudFormation.StackName, stackName);
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
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