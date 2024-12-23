﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Calamari.Testing;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.AWS.CloudFormation
{
    public class CloudFormationFixtureHelpers
    {
        string region;

        public CloudFormationFixtureHelpers(string fixedRegion = null)
        {
            region = fixedRegion ?? RegionRandomiser.GetARegion();
        }

        public static string GetBasicS3Template(string stackName)
        {
            return $@"
    {{
        ""Resources"": {{
            ""{stackName}"": {{
                ""Type"": ""AWS::S3::Bucket"",
                ""Properties"": {{ 
                    ""BucketName"": {stackName},
                    ""Tags"" : [
                        {{
                            ""Key"" : ""VantaOwner"",
                            ""Value"" : ""modern-deployments-team@octopus.com""
                        }},
                        {{
                            ""Key"" : ""VantaNonProd"",
                            ""Value"" : ""true""
                        }},
                        {{
                            ""Key"" : ""VantaNoAlert"",
                            ""Value"" : ""Ephemeral bucket created during unit tests and not used in production""
                        }},
                        {{
                            ""Key"" : ""VantaContainsUserData"",
                            ""Value"" : ""false""
                        }},
                        {{
                            ""Key"" : ""VantaUserDataStored"",
                            ""Value"" : ""N/A""
                        }},
                        {{
                            ""Key"" : ""VantaDescription"",
                            ""Value"" : ""Ephemeral bucket created during unit tests""
                        }}
                    ]
                }}
            }}
        }}
    }}";
        }

        public string WriteTemplateFile(string template)
        {
            var templateFilePath = Path.GetTempFileName();
            File.WriteAllText(templateFilePath, template);
            return templateFilePath;
        }

        public async Task ValidateS3BucketExists(string stackName)
        {
            await ValidateS3(async client =>
            {
                await client.GetBucketLocationAsync(stackName);
            });
        }

        async Task ValidateS3(Func<AmazonS3Client, Task> execute)
        {
            var credentials = new BasicAWSCredentials(await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None),
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));

            var config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
            using (var client = new AmazonS3Client(credentials, config))
            {
                await execute(client);
            }
        }

        public async Task ValidateStackExists(string stackName, bool shouldExist)
        {
            await ValidateCloudFormation(async (client) =>
            {
                Func<IAmazonCloudFormation> clientFactory = () => client;
                var stackStatus = await clientFactory.StackExistsAsync(new StackArn(stackName), Aws.Deployment.Conventions.StackStatus.DoesNotExist);
                stackStatus.Should().Be(shouldExist ? Aws.Deployment.Conventions.StackStatus.Completed : Aws.Deployment.Conventions.StackStatus.DoesNotExist);
            });
        }

        public async Task ValidateStackTags(string stackName, IEnumerable<KeyValuePair<string, string>> expectedTags)
        {
            await ValidateCloudFormation(async (client) =>
            {
                Func<IAmazonCloudFormation> clientFactory = () => client;
                var stack = await clientFactory.DescribeStackAsync(new StackArn(stackName));
                stack.Tags.Should().BeEquivalentTo(expectedTags);
            });
        }
        
        async Task ValidateCloudFormation(Func<AmazonCloudFormationClient, Task> execute)
        {
            var credentials = new BasicAWSCredentials(await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None),
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));

            var config = new AmazonCloudFormationConfig { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
            using (var client = new AmazonCloudFormationClient(credentials, config))
            {
                await execute(client);
            }
        }

        public async Task DeployTemplate(string resourceName, string templateFilePath, IVariables variables)
        {
            var variablesFile = Path.GetTempFileName();
            variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));
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
                    new ExtractPackage(new CombinedPackageExtractor(log, fileSystem, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log),
                    new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                                                         {
                                                             new JsonFormatVariableReplacer(fileSystem, log),
                                                             new XmlFormatVariableReplacer(fileSystem, log),
                                                             new YamlFormatVariableReplacer(fileSystem, log),
                                                             new PropertiesFormatVariableReplacer(fileSystem, log),
                                                         }, variables, fileSystem, log)
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

        public async Task DeployTemplateS3(string resourceName, IVariables variables)
        {
            var variablesFile = Path.GetTempFileName();
            variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));
            variables.Set("Octopus.Action.Aws.Region", "us-east-1");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var log = new InMemoryLog();
                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                var command = new DeployCloudFormationCommand(log,
                                                              variables,
                                                              fileSystem,
                                                              new ExtractPackage(new CombinedPackageExtractor(log, fileSystem, variables, new CommandLineRunner(log, variables)), fileSystem, variables, log),
                                                              new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                                                              {
                                                                  new JsonFormatVariableReplacer(fileSystem, log),
                                                                  new XmlFormatVariableReplacer(fileSystem, log),
                                                                  new YamlFormatVariableReplacer(fileSystem, log),
                                                                  new PropertiesFormatVariableReplacer(fileSystem, log),
                                                              }, variables, fileSystem, log));
                var result = command.Execute(new[]
                {
                    "--templateS3", "https://calamari-cloudformation-tests.s3.amazonaws.com/s3/empty.yaml",
                    "--templateS3Parameters", "https://calamari-cloudformation-tests.s3.amazonaws.com/s3/properties.json",
                    "--variables", $"{variablesFile}",
                    "--stackName", resourceName,
                    "--waitForCompletion", "true"
                });

                result.Should().Be(0);
            }
        }

        public async Task CleanupStack(string stackName)
        {
            try
            {
                await DeleteStack(stackName);
            }
            catch (Exception e)
            {
                Log.Error($"Error occurred while attempting to delete stack {stackName} -> {e}." +
                          $"{Environment.NewLine} Test resources may not have been deleted, please check the AWS console for the status of the stack.");
            }
        }

        public async Task DeleteStack(string stackName)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set("Octopus.Account.AccountType", "AmazonWebServicesAccount");
            variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));
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