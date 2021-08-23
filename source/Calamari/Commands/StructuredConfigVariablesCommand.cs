using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("structured-config-variables")]
    public class StructuredConfigVariablesCommand : Command<StructuredConfigVariablesCommandInputs>
    {
        readonly IVariables variables;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;

        public StructuredConfigVariablesCommand(IVariables variables, IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            this.variables = variables;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
        }

        protected override void Execute(StructuredConfigVariablesCommandInputs inputs)
        {
            var targetPath = variables.GetRaw(inputs.TargetPathVariable);
            if (targetPath == null)
            {
                throw new CommandException($"Could not locate target path from variable {inputs.TargetPathVariable} for {nameof(StructuredConfigVariablesCommand)}");
            }

            structuredConfigVariablesService.ReplaceVariables(targetPath);
        }
    }

    public class StructuredConfigVariablesCommandInputs
    {
        public string TargetPathVariable { get; set; }
    }
}