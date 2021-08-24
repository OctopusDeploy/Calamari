using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationTemplate : ICloudFormationRequestBuilder, ITemplate
    {
        readonly Func<string> content;
        readonly Func<string, List<StackFormationNamedOutput>> parse;

        public CloudFormationTemplate(Func<string> content,
                                      ITemplateInputs<Parameter> parameters,
                                      Func<string, List<StackFormationNamedOutput>> parse)
        {
            this.content = content;
            Inputs = parameters.Inputs;
            this.parse = parse;
        }

        public static CloudFormationTemplate Create(ResolvedTemplatePath path,
                                                    Maybe<ResolvedTemplatePath> parametersPath,
                                                    ICalamariFileSystem fileSystem,
                                                    IVariables variables)
        {
            return new CloudFormationTemplate(() => variables.Evaluate(fileSystem.ReadFile(path.Value)),
                                              CloudFormationParametersFile.Create(parametersPath, fileSystem, variables),
                                              JsonConvert.DeserializeObject<List<StackFormationNamedOutput>>);
        }

        public string Content => content();

        public IEnumerable<Parameter> Inputs { get; }

        public CreateStackRequest BuildCreateStackRequest(string stackName,
                                                          List<string> capabilities,
                                                          bool disableRollback,
                                                          string roleArn,
                                                          List<Tag> tags)
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

        public UpdateStackRequest BuildUpdateStackRequest(string stackName, List<string> capabilities, string roleArn, List<Tag> tags)
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

        public CreateChangeSetRequest BuildChangesetRequest(StackStatus status,
                                                            string changesetName,
                                                            StackArn stack,
                                                            string roleArn,
                                                            List<string> capabilities)
        {
            return new CreateChangeSetRequest
            {
                StackName = stack.Value,
                TemplateBody = Content,
                Parameters = Inputs.ToList(),
                ChangeSetName = changesetName,
                ChangeSetType = status == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
                Capabilities = capabilities,
                RoleARN = roleArn
            };
        }
    }
}