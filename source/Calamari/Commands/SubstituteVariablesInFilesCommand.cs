using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("substitute-variables-in-files")]
    public class SubstituteVariablesInFilesCommand : Command<SubstituteVariablesInFilesCommandInputs>
    {
        readonly IVariables variables;
        readonly ISubstituteInFiles substituteInFiles;

        public SubstituteVariablesInFilesCommand(IVariables variables, ISubstituteInFiles substituteInFiles)
        {
            this.variables = variables;
            this.substituteInFiles = substituteInFiles;
        }

        protected override void Execute(SubstituteVariablesInFilesCommandInputs inputs)
        {
            var runningDeployment = new RunningDeployment(variables);
            var targetPath = runningDeployment.CurrentDirectory;
            
            substituteInFiles.Substitute(targetPath, inputs.FilesToTarget.Split(new []{'\n', '\r', ';'}, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public class SubstituteVariablesInFilesCommandInputs
    {
        public string FilesToTarget { get; set; }
    }
}