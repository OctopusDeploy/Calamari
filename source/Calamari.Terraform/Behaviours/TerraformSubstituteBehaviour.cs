using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Terraform.Behaviours
{
    class TerraformSubstituteBehaviour : IPreDeployBehaviour
    {
        readonly ISubstituteInFiles substituteInFiles;

        public TerraformSubstituteBehaviour(ISubstituteInFiles substituteInFiles)
        {
            this.substituteInFiles = substituteInFiles;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var filesToSubstitute = GetFilesToSubstitute(context.Variables);
            substituteInFiles.Substitute(context.CurrentDirectory, filesToSubstitute);
            return this.CompletedTask();
        }

        string[] GetFilesToSubstitute(IVariables variables)
        {
            var isEnableNoMatchWarningSet = variables.IsSet(PackageVariables.EnableNoMatchWarning);
            var additionalFileSubstitutions = GetAdditionalFileSubstitutions(variables);
            if (!isEnableNoMatchWarningSet)
            {
                var hasAdditionalSubstitutions = !string.IsNullOrEmpty(additionalFileSubstitutions);
                variables.AddFlag(PackageVariables.EnableNoMatchWarning, hasAdditionalSubstitutions);
            }

            var result = new List<string>();

            var runAutomaticFileSubstitution = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.RunAutomaticFileSubstitution, true);
            if (runAutomaticFileSubstitution)
                result.AddRange(new[] { "**/*.tf", "**/*.tf.json", "**/*.tfvars", "**/*.tfvars.json" });

            var additionalFileSubstitution = additionalFileSubstitutions;
            if (!string.IsNullOrWhiteSpace(additionalFileSubstitution))
                result.AddRange(additionalFileSubstitution.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries));

            return result.ToArray();
        }

        string GetAdditionalFileSubstitutions(IVariables variables)
        {
            return variables.Get(TerraformSpecialVariables.Action.Terraform.FileSubstitution);
        }
    }
}