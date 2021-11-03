using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment;

namespace Calamari.Commands
{
    [Command("extract-package")]
    [RetentionLockingCommand]
    public class ExtractPackageCommand : Command<ExtractPackageCommandInputs>
    {
        readonly IExtractPackage extractPackage;

        public ExtractPackageCommand(IExtractPackage extractPackage)
        {
            this.extractPackage = extractPackage;
        }

        protected override void Execute(ExtractPackageCommandInputs inputs)
        {
            extractPackage.ExtractToStagingDirectory(new PathToPackage(inputs.PathToPackage), inputs.ExtractedToPathOutputVariableName);
        }
    }

    public class ExtractPackageCommandInputs
    {
        public string PathToPackage { get; set; }
        public string ExtractedToPathOutputVariableName { get; set; }
    }
}