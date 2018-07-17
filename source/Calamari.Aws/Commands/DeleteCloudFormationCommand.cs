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
    [Command("delete-aws-cloudformation", Description = "Destroy an existing AWS CloudFormation stack")]
    public class DeleteCloudFormationCommand : Command
    {
        private string packageFile;
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private bool waitForComplete;
        
        public DeleteCloudFormationCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("waitForCompletion=", "True if the deployment process should wait for the stack to complete, and False otherwise.", v => waitForComplete =  
                !bool.FalseString.Equals(v, StringComparison.InvariantCultureIgnoreCase)); //True by default
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);
            
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            var environment = new AwsEnvironmentGeneration(variables);
            var stackEventLogger = new StackEventLogger(new LogWrapper());
         
            
            IAmazonCloudFormation ClientFactory () => ClientHelpers.CreateCloudFormationClient(environment);
            StackArn StackProvider (RunningDeployment x) => new StackArn( x.Variables.Get(AwsSpecialVariables.CloudFormation.StackName));
            
            var conventions = new List<IConvention>
            {
                new DeleteCloudFormationStackConvention(environment, stackEventLogger, ClientFactory, StackProvider, waitForComplete)
            };
            
            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            
            conventionRunner.RunConventions();
            return 0;
        }
    }
}