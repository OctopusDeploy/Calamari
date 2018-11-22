using System;
using System.Collections.Specialized;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public class PlanTerraformConvention : TerraformConvention
    {
        public PlanTerraformConvention(ICalamariFileSystem fileSystem, IFileSubstituter fileSubstituter) : base(fileSystem, fileSubstituter)
        {
        }

        protected override void Execute(RunningDeployment deployment, StringDictionary environmentVariables)
        {
            string results;
            using (var cli = new TerraformCLIExecutor(fileSystem, deployment))
            {
                results = cli.ExecuteCommand(environmentVariables, "plan", "-no-color", cli.TerraformVariableFiles, cli.ActionParams);
            }

            Log.Info(
                $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            Log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results);
        }
    }
}