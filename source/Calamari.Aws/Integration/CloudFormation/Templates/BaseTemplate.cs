using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    /// <summary>
    /// Templates capture as much information as possible about the CloudFormation template to be deployed,
    /// and expose methods to build the request objects required by the AWS SDK.
    /// </summary>
    public abstract class BaseTemplate : ICloudFormationRequestBuilder
    {
        protected readonly string stackName;
        protected readonly List<string> capabilities;
        protected readonly bool disableRollback;
        protected readonly StackArn stack;
        protected readonly List<Tag> tags;
        protected readonly string roleArn;
        readonly Func<IAmazonCloudFormation> clientFactory;
        protected readonly IVariables variables;

        public BaseTemplate(IEnumerable<Parameter> inputs,
                            string stackName,
                            List<string> iamCapabilities,
                            bool disableRollback,
                            string roleArn,
                            IEnumerable<KeyValuePair<string, string>> tags,
                            StackArn stack,
                            Func<IAmazonCloudFormation> clientFactory,
                            IVariables variables)
        {
            Inputs = inputs;

            this.stackName = stackName;
            this.disableRollback = disableRollback;
            this.stack = stack;
            this.roleArn = roleArn;
            this.clientFactory = clientFactory;
            this.variables = variables;
            this.tags = tags?.Select(x => new Tag { Key = x.Key, Value = x.Value }).ToList();

            var (validCapabilities, _) = ExcludeAndLogUnknownIamCapabilities(iamCapabilities);
            capabilities = validCapabilities.ToList();
        }

        protected async Task<StackStatus> GetStackStatus()
        {
            return await clientFactory.StackExistsAsync(stack, StackStatus.DoesNotExist);
        }

        /// <summary>
        /// https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/using-iam-template.html#capabilities
        /// </summary>
        (IList<string> valid, IList<string> excluded) ExcludeAndLogUnknownIamCapabilities(IEnumerable<string> values)
        {
            var (valid, excluded) = ExcludeUnknownIamCapabilities(values);
            if (excluded.Count > 0)
            {
                Log.Warn($"The following unknown IAM Capabilities have been removed: {String.Join(", ", excluded)}");
            }

            return (valid, excluded);
        }

        (IList<string> valid, IList<string> excluded) ExcludeUnknownIamCapabilities(
            IEnumerable<string> capabilities)
        {
            return capabilities.Aggregate((new List<string>(), new List<string>()),
                                          (prev, current) =>
                                          {
                                              var (valid, excluded) = prev;

                                              if (current.IsKnownIamCapability())
                                                  valid.Add(current);
                                              else
                                                  excluded.Add(current);

                                              return prev;
                                          });
        }

        public IEnumerable<Parameter> Inputs { get; }
        public abstract CreateStackRequest BuildCreateStackRequest();
        public abstract UpdateStackRequest BuildUpdateStackRequest();
        public abstract Task<CreateChangeSetRequest> BuildChangesetRequest();
    }
}