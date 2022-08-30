using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.LaunchTools;
using Calamari.Serialization;
using Newtonsoft.Json;

namespace Calamari.Commands
{
    [Command("execute-manifest")]
    public class ExecuteManifestCommand : Command
    {
        readonly IVariables variables;
        readonly IEnumerable<Meta<ILaunchTool, LaunchToolMeta>> executionTools;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;
        private readonly ICommandLineRunner commandLineRunner;

        public ExecuteManifestCommand(IVariables variables,
            IEnumerable<Meta<ILaunchTool, LaunchToolMeta>> executionTools,
            ICalamariFileSystem fileSystem,
            ILog log, ICommandLineRunner commandLineRunner)
        {
            this.variables = variables;
            this.executionTools = executionTools;
            this.fileSystem = fileSystem;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var contents = variables.Get(SpecialVariables.Execution.Manifest);

            if (contents == null)
            {
                throw new CommandException("Execution manifest not found in variables.");
            }

            var instructions =
                JsonConvert.DeserializeObject<Instruction[]>(contents,
                    JsonSerialization.GetDefaultSerializerSettings());

            if (instructions.Length == 0)
            {
                throw new CommandException("The execution manifest must have at least one instruction.");
            }

            foreach (var instruction in instructions)
            {
                var tool = executionTools.First(x => x.Metadata.Tool == instruction.Launcher);

                var result = tool.Value.Execute(instruction.LauncherInstructionsRaw);

                if (result != 0)
                {
                    return result;
                }

                if (variables.GetFlag(KnownVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
            }

            return 0;
        }
    }
}