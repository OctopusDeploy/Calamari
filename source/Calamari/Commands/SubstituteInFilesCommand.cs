using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("substitute-in-files")]
    public class SubstituteInFilesCommand : Command<SubstituteInFilesCommandInputs>
    {
        readonly IVariables variables;
        readonly ISubstituteInFiles substituteInFiles;

        public SubstituteInFilesCommand(IVariables variables, ISubstituteInFiles substituteInFiles)
        {
            this.variables = variables;
            this.substituteInFiles = substituteInFiles;
        }

        protected override int Execute(SubstituteInFilesCommandInputs inputs)
        {
            var targetPath = variables.GetRaw(inputs.TargetPathVariable);
            if (targetPath == null)
            {
                throw new CommandException($"Could not locate target path from variable {inputs.TargetPathVariable} for {nameof(SubstituteInFilesCommand)}");
            }

            substituteInFiles.Substitute(targetPath, inputs.FilesToTarget);
            return 0;
        }
    }

    public class SubstituteInFilesCommandInputs
    {
        public string TargetPathVariable { get; set; }
        public string[] FilesToTarget { get; set; }
    }
}