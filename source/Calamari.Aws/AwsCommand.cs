using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Util;
using Calamari.Commands.Support;

namespace Calamari.Aws
{
    public abstract class AwsCommand : ICommand, IDisposable
    {
        static readonly Regex ArnNameRe = new Regex("^.*?/(.+)$");

        protected readonly ILog log;
        protected readonly IVariables variables;

        readonly Lazy<Task<IAmazonIdentityManagementService>> amazonIdentityManagementClient;
        readonly Lazy<Task<IAmazonSecurityTokenService>> amazonSecurityTokenClient;

        protected AwsCommand(
            ILog log,
            IVariables variables,
            IAmazonClientFactory amazonClientFactory)
        {
            this.log = log;
            this.variables = variables;

            amazonIdentityManagementClient = new Lazy<Task<IAmazonIdentityManagementService>>(amazonClientFactory.GetIdentityManagementClient);
            amazonSecurityTokenClient = new Lazy<Task<IAmazonSecurityTokenService>>(amazonClientFactory.GetSecurityTokenClient);
        }

        public int Execute()
        {
            LogAwsUserInfo().ConfigureAwait(false).GetAwaiter().GetResult();

            ExecuteCoreAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            return 0;
        }

        protected abstract Task ExecuteCoreAsync();

        protected void SetOutputVariable(string name, string value)
        {
            log.SetOutputVariable($"AwsOutputs[{name}]", value ?? "", variables);
            log.Info($"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.AwsOutputs[{name}]\"");
        }

        protected void SetOutputVariables(IReadOnlyCollection<VariableOutput> variableOutputs)
        {
            if (variableOutputs?.Any() != true) return;

            foreach (var variableOutput in variableOutputs)
            {
                SetOutputVariable(variableOutput.Name, variableOutput.Value);
            }
        }

        Task LogAwsUserInfo()
        {
            var isAssumeRoleArnSet = variables.IsSet(SpecialVariableNames.Aws.AssumeRoleARN);
            var isAccountIdSet = variables.IsSet(SpecialVariableNames.Aws.AccountId);
            var isAccountAccessKeySet = variables.IsSet($"{SpecialVariableNames.Aws.AccountId}.AccessKey");

            var isUserRoleLocatable = isAssumeRoleArnSet || !isAccountIdSet || !isAccountAccessKeySet;

            return isUserRoleLocatable ? TryLogAwsUserRole() : TryLogAwsUserName();
        }

        async Task TryLogAwsUserName()
        {
            try
            {
                var client = await amazonIdentityManagementClient.Value;

                var result = await client.GetUserAsync(new GetUserRequest());

                log.Info($"Running the step as the AWS user {result.User.UserName}");
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }

        async Task TryLogAwsUserRole()
        {
            try
            {
                var client = await amazonSecurityTokenClient.Value;

                var response = await client.GetCallerIdentityAsync(new GetCallerIdentityRequest());

                var match = ArnNameRe.Match(response.Arn);

                var awsRole = match.Success ? match.Groups[1].Value : "Unknown";

                log.Info($"Running the step as the AWS role {awsRole}");
            }
            catch (AmazonServiceException)
            {
                // Ignore, we just won't add this to the logs
            }
        }

        public virtual void Dispose()
        {
            if (amazonIdentityManagementClient.IsValueCreated) amazonIdentityManagementClient.Value.Dispose();

            if (amazonSecurityTokenClient.IsValueCreated) amazonSecurityTokenClient.Value.Dispose(); 
        }
    }
}