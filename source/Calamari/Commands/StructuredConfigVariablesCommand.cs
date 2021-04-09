using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("structured-config-variables")]
    public class StructuredConfigVariablesCommand : Command
    {
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        readonly string targetPath;

        public StructuredConfigVariablesCommand(IVariables variables, IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            targetPath = variables.Get(PackageVariables.Output.InstallationDirectoryPath, String.Empty);
            this.structuredConfigVariablesService = structuredConfigVariablesService;
        }

        public override int Execute(string[] commandLineArguments)
        {
            structuredConfigVariablesService.ReplaceVariables(targetPath);
            return 0;
        }
    }
}