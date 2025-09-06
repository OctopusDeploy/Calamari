using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Newtonsoft.Json;

namespace Calamari.Aws.Commands
{
    [Command("create-aws-s3", Description = "Creates an AWS S3 bucket")]
    public class CreateAwsS3Command : Command
    {
        readonly ILog log;
        readonly IVariables variables;

        public CreateAwsS3Command(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var bucketName = variables.Get(AwsSpecialVariables.S3.BucketName);
            var stackName = variables.Get(AwsSpecialVariables.CloudFormation.StackName);
            var publicAccess = variables.GetFlag(AwsSpecialVariables.S3.PublicAccess);
            var objectWriterOwnership = variables.GetFlag(AwsSpecialVariables.S3.ObjectWriterOwnership);
            
            Guard.NotNullOrWhiteSpace(bucketName, "Bucket name is required");
            Guard.NotNullOrWhiteSpace(stackName, "Stack name is required");

            var tags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]");
            ValidateTags(tags);

            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();

            IAmazonCloudFormation ClientFactory() => ClientHelpers.CreateCloudFormationClient(environment);

            StackArn StackProvider(RunningDeployment _) => new StackArn(stackName);

            ICloudFormationRequestBuilder TemplateFactory()
            {
                return new CloudFormationTemplate(() => GetTemplateBody(bucketName, publicAccess, objectWriterOwnership),
                                                  new EmptyTemplateInputs<Parameter>(),
                                                  stackName,
                                                  new List<string>(),
                                                  true,
                                                  null,
                                                  tags,
                                                  new StackArn(stackName),
                                                  ClientFactory,
                                                  variables);
            }

            var stackEventLogger = new StackEventLogger(log);

            var conventions = new List<IConvention>
            {
                new LogAwsUserInfoConvention(environment),
                new DeployAwsCloudFormationConvention(
                                                      ClientFactory,
                                                      TemplateFactory,
                                                      stackEventLogger,
                                                      StackProvider,
                                                      _ => null,
                                                      true,
                                                      stackName,
                                                      environment,
                                                      log),
                new DescribeAwsCloudFormationStackConvention(ClientFactory,
                                                             StackProvider,
                                                             ExtractBucketName,
                                                             stackEventLogger, log)
            };

            var conventionRunner = new ConventionProcessor(new RunningDeployment(variables, new NonSensitiveCalamariVariables()), conventions, log);
            conventionRunner.RunConventions();

            return 0;
        }

        void ValidateTags(List<KeyValuePair<string, string>> tags)
        {
            var errorLink = log.FormatShortLink("createAwsS3BucketValidationError", "S3-CreateBucket-ValidationError");
            if (tags.Count > 20)
                throw new CommandException($"{errorLink} Total number of tags must not be more than 20.");

            var uniqueTagKeys = tags.Select(t => t.Key).Distinct();
            if (uniqueTagKeys.Count() != tags.Count)
                throw new CommandException($"{errorLink} Each tag key must be unique.");
        }

        string GetTemplateBody(string bucketName, bool publicAccess, bool objectWriterOwnership)
        {
            var template = new
            {
                AWSTemplateFormatVersion = "2010-09-09",
                Resources = new Dictionary<string, object>
                {
                    [$"Bucket{bucketName.ToCamelCase()}"] = new
                    {
                        Type = "AWS::S3::Bucket",
                        Properties = new
                        {
                            BucketName = bucketName,
                            PublicAccessBlockConfiguration = new
                            {
                                BlockPublicAcls = !publicAccess,
                                BlockPublicPolicy = !publicAccess,
                                IgnorePublicAcls = !publicAccess,
                                RestrictPublicBuckets = !publicAccess
                            },
                            OwnershipControls = new
                            {
                                Rules = new object[]
                                {
                                    new
                                    {
                                        ObjectOwnership = objectWriterOwnership ? "ObjectWriter" : "BucketOwnerPreferred"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return JsonConvert.SerializeObject(template);
        }

        List<KeyValuePair<string, string>> ExtractBucketName(List<StackResourceSummary> resourceSummaries)
        {
            var result = new List<KeyValuePair<string, string>>();
            var bucket = resourceSummaries.FirstOrDefault(x => x.ResourceType == "AWS::S3::Bucket");
            if (bucket != null)
                result.Add(new KeyValuePair<string, string>("BucketName", bucket.PhysicalResourceId));
            return result;
        }
    }
}