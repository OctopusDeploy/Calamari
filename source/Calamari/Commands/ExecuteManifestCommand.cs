using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
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

        public ExecuteManifestCommand(
            IVariables variables,
            IEnumerable<Meta<ILaunchTool, LaunchToolMeta>> executionTools)
        {
            this.variables = variables;
            this.executionTools = executionTools;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var contents = variables.Get(SpecialVariables.Execution.Manifest);

            if (contents == null)
            {
                throw new Exception("Execution manifest not found in variables.");
            }

            var instructions = JsonConvert.DeserializeObject<Instruction[]>(contents, JsonSerialization.GetDefaultSerializerSettings());

            if (instructions.Length == 0)
            {
                throw new Exception("The execution manifest must have at least one instruction.");
            }

            foreach (var instruction in instructions)
            {
                var tool = executionTools.First(x => x.Metadata.Tool == instruction.Launcher);

                var result = tool.Value.Execute(instruction.LauncherInstructionsRaw, commandLineArguments.Skip(1).ToArray());

                if (result != 0) return result;
            }

            return 0;
        }
    }
}