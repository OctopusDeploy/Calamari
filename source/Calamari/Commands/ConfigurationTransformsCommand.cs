using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("configuration-transforms")]
    public class ConfigurationTransformsCommand : Command
    {
        readonly ConfigurationTransformsBehaviour substituteInFiles;
        readonly string targetPath;

        public ConfigurationTransformsCommand(IVariables variables, ConfigurationTransformsBehaviour substituteInFiles)
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