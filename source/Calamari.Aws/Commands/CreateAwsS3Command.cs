using System;
using System.Collections.Generic;
using System.Globalization;
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
        string bucketName;
        string stackName;
        bool publicAccess;
        bool objectWriterOwnership;

        public CreateAwsS3Command(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
            Options.Add("bucketName=", "The name of the bucket to create", v => bucketName = v);
            Options.Add("stackName=", "The name of the CloudFormation stack.", v => stackName = v);
            Options.Add("publicAccess=",
                        "True if the bucket should allow public access, and False otherwise.",
                        v => publicAccess = bool.TrueString.Equals(v, StringComparison.OrdinalIgnoreCase)); // False by default
            Options.Add("objectWriterOwnership=",
                        "True if the account creating the object should own it, and False if the bucket should own the object.",
                        v => objectWriterOwnership = bool.TrueString.Equals(v, StringComparison.OrdinalIgnoreCase)); // False by default
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var tags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags) ?? "[]");
            ValidateTags(tags);
            
            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();

            IAmazonCloudFormation ClientFactory()
            {
                return ClientHelpers.CreateCloudFormationClient(environment);
            }

            StackArn StackProvider(RunningDeployment _) => new StackArn(stackName);

            ICloudFormationRequestBuilder TemplateFactory()
            {
                return new CloudFormationTemplate(GetTemplateBody,
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
                                                      environment)
            };
            
            var conventionRunner = new ConventionProcessor(new RunningDeployment(variables), conventions, log);
            conventionRunner.RunConventions();
            return 0;

            // if stack is in update progress, fail step -- we might not need it manually but let the AWS error message bubble up
            // deploy stack with strategy: create or update or delete+recreate (for unrecoverable states)

            // print output variables
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

        string GetTemplateBody()
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
                                Rules = new object []
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
    }
}