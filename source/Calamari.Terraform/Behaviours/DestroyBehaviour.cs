using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Terraform.Behaviours
{
    class DestroyBehaviour : TerraformDeployBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public DestroyBehaviour(ILog log,
                                ICalamariFileSystem fileSystem,
                                ICommandLineRunner commandLineRunner) : base(log)
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(log,
                                                      fileSystem,
                                                      commandLineRunner,
                                                      deployment,
                                                      environmentVariables))
            {
                var args = new List<string>();
                args.Add("destroy");
                args.Add("-auto-approve");
                if (!OctopusFeatureToggles.AnsiColorsInTaskLogFeatureToggle.IsEnabled(deployment.Variables))
                    args.Add("-no-color");
                args.Add(cli.TerraformVariableFiles);
                args.Add(cli.ActionParams);
                cli.ExecuteCommand(args.ToArray())
                   .VerifySuccess();
            }

            return Task.CompletedTask;
        }
    }
}
