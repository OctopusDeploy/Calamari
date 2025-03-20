using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Tag = Amazon.S3.Model.Tag;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CreateAwsS3CommandFixture
    {
        const string Region = "ap-southeast-2";
        readonly CancellationToken cancellationToken;
        readonly string stackName;
        readonly string bucketName;
        readonly CalamariVariables variables;

        readonly List<KeyValuePair<string, string>> customTags = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("VantaOwner", "modern-deployments-team@octopus.com"),
            new KeyValuePair<string, string>("VantaNonProd", "true"),
            new KeyValuePair<string, string>("VantaNoAlert", "Ephemeral bucket created during unit tests and not used in production"),
            new KeyValuePair<string, string>("VantaContainsUserData", "false"),
            new KeyValuePair<string, string>("VantaUserDataStored", "N/A"),
            new KeyValuePair<string, string>("VantaDescription", "Ephemeral bucket created during unit tests")
        };

        public CreateAwsS3CommandFixture()
        {
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            cancellationToken = cancellationTokenSource.Token;
            stackName = $"calamari-create-s3-stack-{Guid.NewGuid()}";
            bucketName = $"calamari-create-s3-bucket-{Guid.NewGuid()}";
            variables = new CalamariVariables();
        }

        [SetUp]
        public async Task Setup()
        {
            variables.Set(AwsSpecialVariables.Authentication.AwsAccountVariable, "AWSAccount");
            variables.Set(SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set("Octopus.Action.Aws.Region", Region);
            variables.Set("AWSAccount.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken));
            variables.Set("AWSAccount.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));
        }

        [TearDown]
        public async Task TearDownInfrastructure()
        {
            await ExecuteAwsClient(async (s3Client, cfClient) =>
                                   {
                                       var response = await s3Client.ListObjectsAsync(bucketName, cancellationToken);
                                       foreach (var s3Object in response.S3Objects)
                                           await s3Client.DeleteObjectAsync(bucketName, s3Object.Key, cancellationToken);
                                       await cfClient.DeleteStackAsync(new DeleteStackRequest
                                                                       {
                                                                           StackName = stackName
                                                                       },
                                                                       cancellationToken);
                                   });
        }

        [Test]
        public void NonUniqueTags_ShouldThrow()
        {
            var tags = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "value1"),
                new KeyValuePair<string, string>("key1", "value2")
            };
            variables.Set(AwsSpecialVariables.CloudFormation.Tags, JsonConvert.SerializeObject(tags));

            var command = new CreateAwsS3Command(new InMemoryLog(), variables);
            Action act = () => command.Execute(new[]
            {
                "--bucket", bucketName
            });

            act.Should().Throw<CommandException>().WithMessage("*Each tag key must be unique.");
        }

        [Test]
        public async Task BucketShouldBeCreated()
        {
            // Arrange
            variables.Set(AwsSpecialVariables.CloudFormation.Tags, JsonConvert.SerializeObject(customTags));

            var log = new InMemoryLog();
            var command = new CreateAwsS3Command(log, variables);

            // Act
            var result = command.Execute(new[]
            {
                "--bucketName", bucketName,
                "--stackName", stackName,
                "--objectWriterOwnership", "False"
            });

            // Assert
            result.Should().Be(0);
            await ValidateS3(ObjectOwnership.BucketOwnerPreferred);
            CheckForOutputVariables();
        }

        [Test]
        public async Task BucketWithSameIdentifiersShouldBeUpdated()
        {
            // Arrange
            variables.Set(AwsSpecialVariables.CloudFormation.Tags, JsonConvert.SerializeObject(customTags));

            var log = new InMemoryLog();

            // Act
            new CreateAwsS3Command(log, variables).Execute(new[]
            {
                "--bucketName", bucketName,
                "--stackName", stackName,
                "--objectWriterOwnership", "False"
            });
            var result = new CreateAwsS3Command(log, variables).Execute(new[]
            {
                "--bucketName", bucketName,
                "--stackName", stackName,
                "--objectWriterOwnership", "True"
            });

            // Assert
            result.Should().Be(0);
            await ValidateS3(ObjectOwnership.ObjectWriter);
            CheckForOutputVariables();
        }

        async Task ValidateS3(ObjectOwnership expectedObjectOwnership)
        {
            await ExecuteAwsClient(async (s3Client, cfClient) =>
                                   {
                                       var resourcesResponse = await cfClient.ListStackResourcesAsync(new ListStackResourcesRequest
                                                                                                      {
                                                                                                          StackName = stackName
                                                                                                      },
                                                                                                      cancellationToken);
                                       resourcesResponse.StackResourceSummaries.Should().HaveCount(1);
                                       resourcesResponse.StackResourceSummaries[0].ResourceType.Should().Be("AWS::S3::Bucket");
                                       resourcesResponse.StackResourceSummaries[0].LogicalResourceId.Should().StartWith("BucketcalamariCreateS3Bucket");

                                       var bucketTagsResponse = await s3Client.GetBucketTaggingAsync(new GetBucketTaggingRequest
                                                                                                     {
                                                                                                         BucketName = bucketName
                                                                                                     },
                                                                                                     cancellationToken);
                                       var tagList = customTags.Select(t => new Tag { Key = t.Key, Value = t.Value }).ToList();
                                       bucketTagsResponse.TagSet.Where(t => tagList.Exists(x => x.Key == t.Key)).Should().BeEquivalentTo(tagList);

                                       var bucketOwnershipControlsResponse = await s3Client.GetBucketOwnershipControlsAsync(new GetBucketOwnershipControlsRequest
                                                                                                                            {
                                                                                                                                BucketName = bucketName
                                                                                                                            },
                                                                                                                            cancellationToken);
                                       bucketOwnershipControlsResponse.OwnershipControls.Rules.Should()
                                                                      .BeEquivalentTo(new OwnershipControlsRule
                                                                      {
                                                                          ObjectOwnership = expectedObjectOwnership
                                                                      });
                                   });
        }

        async Task ExecuteAwsClient(Func<AmazonS3Client, AmazonCloudFormationClient, Task> execute)
        {
            var credentials = new BasicAWSCredentials(
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken),
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));

            var s3Config = new AmazonS3Config { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(Region), LogResponse = true };
            var cfConfig = new AmazonCloudFormationConfig { AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(Region), LogResponse = true };

            using (var cfClient = new AmazonCloudFormationClient(credentials, cfConfig))
            using (var s3Client = new AmazonS3Client(credentials, s3Config))
            {
                await execute(s3Client, cfClient);
            }
        }

        void CheckForOutputVariables()
        {
            variables.Select(x => x.Key).Should().Contain("StackName", "StackId", "BucketName", "Region");
        }
    }
}