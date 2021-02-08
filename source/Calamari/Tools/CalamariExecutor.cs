using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;

namespace Calamari.Tools
{
    [ExecutionTool(ExecutionTool.Calamari)]
    public class CalamariExecutor : IExecuteTool
    {
        readonly IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>> commands;

        public CalamariExecutor(IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>> commands)
        {
            this.commands = commands;
        }

        public int Execute(string command, string inputs, string[] commandLineArguments)
        {
            var commandToExecute = commands.Single(x => x.Metadata.Name.Equals(command, StringComparison.OrdinalIgnoreCase));

            return commandToExecute.Value.Value.Execute(commandLineArguments.Skip(0).ToArray());
        }
    }
}