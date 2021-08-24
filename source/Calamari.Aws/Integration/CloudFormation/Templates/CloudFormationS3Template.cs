using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Octopus.CoreUtilities;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;
using Tag = Amazon.CloudFormation.Model.Tag;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationS3Template : ICloudFormationRequestBuilder
    {
        const string ParametersFile = "parameters.json";

        public CloudFormationS3Template(ITemplateInputs<Parameter> parameters,
                                        string templateS3Url)
        {
            Inputs = parameters.Inputs;
            TemplateS3Url = templateS3Url;
        }

        public static CloudFormationS3Template Create(string templateS3Url,
                                                                  string templateParameterS3Url,
                                                                  ICalamariFileSystem fileSystem,
                                                                  IVariables variables,
                                                                  ILog log)
        {
            var templatePath = string.IsNullOrWhiteSpace(templateParameterS3Url)
                ? Maybe<ResolvedTemplatePath>.None
                : new ResolvedTemplatePath(ParametersFile).AsSome();

            if (templatePath.Some())
            {
                DownloadS3(variables, log, templateParameterS3Url);
            }

            var parameters = CloudFormationParametersFile.CreateUnprocessed(templatePath, fileSystem);

            return new CloudFormationS3Template(parameters, templateS3Url);
        }

        /// <summary>
        /// The SDK allows us to deploy a template from a URL, but does not apply parameters from a URL. So we
        /// must download the parameters file and parse it locally.
        /// </summary>
        static void DownloadS3(IVariables variables, ILog log, string templateParameterS3Url)
        {
            try
            {
                var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
                var s3Uri = new AmazonS3Uri(templateParameterS3Url);
                using (IAmazonS3 client = ClientHelpers.CreateS3Client(environment))
                {
                    var request = new GetObjectRequest
                    {
                        BucketName = s3Uri.Bucket,
                        Key = s3Uri.Key
                    };
                    var response = client.GetObjectAsync(request).GetAwaiter().GetResult();
                    response.WriteResponseStreamToFileAsync(ParametersFile, false, new CancellationTokenSource().Token).GetAwaiter().GetResult();
                }
            }
            catch (UriFormatException ex)
            {
                log.Error($"The parameters URL of {templateParameterS3Url} is invalid");
                throw ex;
            }
        }

        string TemplateS3Url { get; }
        public IEnumerable<Parameter> Inputs { get; }

        public CreateStackRequest BuildCreateStackRequest(string stackName, List<string> capabilities, bool disableRollback, string roleArn, List<Tag> tags)
        {
            return new CreateStackRequest
            {
                StackName = stackName,
                TemplateURL = TemplateS3Url,
                Parameters = Inputs.ToList(),
                Capabilities = capabilities,
                DisableRollback = disableRollback,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public UpdateStackRequest BuildUpdateStackRequest(string stackName, List<string> capabilities, string roleArn, List<Tag> tags)
        {
            return new UpdateStackRequest
            {
                StackName = stackName,
                TemplateURL = TemplateS3Url,
                Parameters = Inputs.ToList(),
                Capabilities = capabilities,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public CreateChangeSetRequest BuildChangesetRequest(StackStatus status,
                                                            string changesetName,
                                                            StackArn stack,
                                                            string roleArn,
                                                            List<string> capabilities)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateURL = TemplateS3Url,
                Parameters = Inputs.ToList(),
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                Capabilities = capabilities,
                RoleARN = roleArn
            };
        }
    }
}