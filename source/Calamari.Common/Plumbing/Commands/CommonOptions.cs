using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands.Options;

namespace Calamari.Common.Plumbing.Commands
{
    public class CommonOptions
    {
        internal CommonOptions(string command)
        {
            Command = command;
        }

        public string Command { get; }
        public List<string> RemainingArguments { get; private set; } = new List<string>();
        public Variables InputVariables { get; } = new Variables();

        public static CommonOptions Parse(string[] args)
        {
            if (args.Length == 0)
                throw new CommandException("No command to run specified as a parameter");

            var command = args[0];
            if (string.IsNullOrWhiteSpace(command) || command.StartsWith("-"))
                throw new CommandException($"The command to run '{command}' is not a valid command name");

            var options = new CommonOptions(command);

            var set = new OptionSet()
                .Add("variables=", "Path to a JSON file containing variables.", v => options.InputVariables.VariablesFile = v)
                .Add("outputVariables=", "Base64 encoded encrypted JSON file containing output variables.", v => options.InputVariables.OutputVariablesFile = v)
                .Add("outputVariablesPassword=", "Password used to decrypt output-variables", v => options.InputVariables.OutputVariablesPassword = v)
                .Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => options.InputVariables.SensitiveVariablesFiles.Add(v))
                .Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => options.InputVariables.SensitiveVariablesPassword = v);
            options.RemainingArguments = set.Parse(args.Skip(1));

            return options;
        }

        public class Variables
        {
            public string? VariablesFile { get; internal set; }
            public List<string> SensitiveVariablesFiles { get; } = new List<string>();
            public string? SensitiveVariablesPassword { get; internal set; }
            public string? OutputVariablesFile { get; internal set; }
            public string? OutputVariablesPassword { get; internal set; }
        }
    }
}