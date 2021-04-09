using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("configuration-variables")]
    public class ConfigurationVariablesCommand : Command
    {
        readonly ConfigurationVariablesBehaviour substituteInFiles;
        readonly string targetPath;

        public ConfigurationVariablesCommand(IVariables variables, ConfigurationVariablesBehaviour substituteInFiles)
        {
            targetPath = variables.Get(PackageVariables.Output.InstallationDirectoryPath, String.Empty);
            this.substituteInFiles = substituteInFiles;
        }

        public override int Execute(string[] commandLineArguments)
        {
            substituteInFiles.DoTransforms(targetPath);
            return 0;
        }
    }
}