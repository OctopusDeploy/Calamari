using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationTemplate : BaseTemplate, ITemplate
    {
        readonly Func<string> content;

        public CloudFormationTemplate(Func<string> content,
                                      ITemplateInputs<Parameter> parameters,
                                      string stackName,
                                      List<string> iamCapabilities,
                                      bool disableRollback,
                                      string roleArn,
                                      IEnumerable<KeyValuePair<string, string>> tags,
                                      StackArn stack,
                                      Func<IAmazonCloudFormation> clientFactory,
                                      IVariables variables) : base(parameters.Inputs,
                                                                                        stackName,
                                                                                        iamCapabilities,
                                                                                        disableRollback,
                                                                                        roleArn,
                                                                                        tags,
                                                                                        stack,
                                                                                        clientFactory,
                                                                                        variables)
        {
            this.content = content;
        }

        public static ICloudFormationRequestBuilder Create(ITemplateResolver templateResolver,
                                                           string templateFile,
                                                           string templateParameterFile,
                                                           bool filesInPackage,
                                                           ICalamariFileSystem fileSystem,
                                                           IVariables variables,
                                                           string stackName,
                                                           List<string> capabilities,
                                                           bool disableRollback,
                                                           string roleArn,
                                                           IEnumerable<KeyValuePair<string, string>> tags,
                                                           StackArn stack,
                                                           Func<IAmazonCloudFormation> clientFactory)
        {
            var resolvedTemplate = templateResolver.Resolve(templateFile, filesInPackage, variables);
            var resolvedParameters = templateResolver.MaybeResolve(templateParameterFile, filesInPackage, variables);

            if (!string.IsNullOrWhiteSpace(templateParameterFile) && !resolvedParameters.Some())
                throw new CommandException("Could not find template parameters file: " + templateParameterFile);

            return new CloudFormationTemplate(() => variables.Evaluate(fileSystem.ReadFile(resolvedTemplate.Value)),
                                              CloudFormationParametersFile.Create(resolvedParameters, fileSystem, variables),
                                              stackName,
                                              capabilities,
                                              disableRollback,
                                              roleArn,
                                              tags,
                                              stack,
                                              clientFactory,
                                              variables);
        }

        public string Content => content();

        public override CreateStackRequest BuildCreateStackRequest()
        {
            return new CreateStackRequest
            {
                StackName = stackName,
                TemplateBody = Content,
                Parameters = Inputs.ToList(),
                Capabilities = capabilities,
                DisableRollback = disableRollback,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public override UpdateStackRequest BuildUpdateStackRequest()
        {
            return new UpdateStackRequest
            {
                StackName = stackName,
                TemplateBody = Content,
                Parameters = Inputs.ToList(),
                Capabilities = capabilities,
                RoleARN = roleArn,
                Tags = tags
            };
        }

        public override async Task<CreateChangeSetRequest> BuildChangesetRequest()
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateBody = Content,
                Parameters = Inputs.ToList(),
                /*
                 * The change set name might be passed down directly, or this variable may be
                 * set as part of the deployment. Reading the value from the variables here
                 * allows us to catch any deferred construction of the change stack name.
                 */
                ChangeSetName = variables[AwsSpecialVariables.CloudFormation.Changesets.Name],
                ChangeSetType = await GetStackStatus() == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                Capabilities = capabilities,
                RoleARN = roleArn
            };
        }
    }
}