using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Calamari)]
    public class CalamariExecutor : LaunchTool<CalamariInstructions>
    {
        readonly IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>> commands;

        public CalamariExecutor(IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>> commands)
        {
            this.commands = commands;
        }

        protected override int ExecuteInternal(CalamariInstructions instructions, params string[] args)
        {
            var commandToExecute = commands.Single(x => x.Metadata.Name.Equals(instructions.Command, StringComparison.OrdinalIgnoreCase));

            return commandToExecute.Value.Value.Execute(args);
        }
    }

    public class CalamariInstructions
    {
        public string Command { get; set; }
    }
}