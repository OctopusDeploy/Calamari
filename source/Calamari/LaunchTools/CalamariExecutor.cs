using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json.Linq;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Calamari)]
    public class CalamariExecutor : LaunchTool<CalamariInstructions>
    {
        readonly IEnumerable<Meta<Lazy<ICommandWithInputs>, CommandMeta>> commands;

        public CalamariExecutor(IEnumerable<Meta<Lazy<ICommandWithInputs>, CommandMeta>> commands)
        {
            this.commands = commands;
        }

        protected override int ExecuteInternal(CalamariInstructions instructions)
        {
            var commandToExecute = commands.Single(x => x.Metadata.Name.Equals(instructions.Command, StringComparison.OrdinalIgnoreCase));

            return commandToExecute.Value.Value.Execute(instructions.Inputs.ToString());
        }
    }

    public class CalamariInstructions
    {
        public string Command { get; set; }
        public JObject Inputs { get; set; }
    }
}