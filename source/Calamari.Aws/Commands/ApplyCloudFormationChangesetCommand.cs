using System;
using System.Collections.Generic;
using System.IO;
using Amazon.CloudFormation;
using Calamari.Aws.Deployment;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Aws.Util;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;

namespace Calamari.Aws.Commands
{
    [Command("apply-aws-cloudformation-changeset", Description = "Apply an existing AWS CloudFormation changeset")]
    public class ApplyCloudFormationChangesetCommand: Command
    {
        readonly IVariables variables;
        readonly ILog log;
        private string packageFile;
        private bool waitForComplete;
        
        public ApplyCloudFormationChangesetCommand(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("waitForCompletion=", "True if the deployment process should wait for the stack to complete, and False otherwise.", v => waitForComplete =  
                !bool.FalseString.Equals(v, StringComparison.OrdinalIgnoreCase)); //True by default
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var environment = AwsEnvironmentGeneration.Create(variables, log).GetAwaiter().GetResult();
            var stackEventLogger = new StackEventLogger(new LogWrapper());

            IAmazonCloudFormation ClientFactory () => ClientHelpers.CreateCloudFormationClient(environment);
            StackArn StackProvider (RunningDeployment x) => new StackArn(x.Variables.Get(AwsSpecialVariables.CloudFormation.StackName));
            ChangeSetArn ChangesetProvider (RunningDeployment x) => new ChangeSetArn(x.Variables[AwsSpecialVariables.CloudFormation.Changesets.Arn]);

            var conventions = new List<IConvention>
            {
                new LogAwsUserInfoConvention(environment),
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExecuteCloudFormationChangeSetConvention(ClientFactory, stackEventLogger, StackProvider, ChangesetProvider, waitForComplete),
                new CloudFormationOutputsAsVariablesConvention(ClientFactory, stackEventLogger, StackProvider)
            };
            
            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            
            conventionRunner.RunConventions();
            return 0;
        }
    }
}