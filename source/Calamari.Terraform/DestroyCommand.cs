using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public DestroyCommand(ILog log,
                              IVariables variables,
                              ICalamariFileSystem fileSystem,
                              ICommandLineRunner commandLineRunner,
                              ISubstituteInFiles substituteInFiles,
                              IExtractPackage extractPackage)
            : base(log,
                   variables,
                   fileSystem,
                   substituteInFiles,
                   extractPackage)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(log,
                                                      fileSystem,
                                                      commandLineRunner,
                                                      deployment,
                                                      environmentVariables))
            {
                cli.ExecuteCommand("destroy",
                                   "-force",
                                   "-no-color",
                                   cli.TerraformVariableFiles,
                                   cli.ActionParams)
                   .VerifySuccess();
            }
        }
    }
}