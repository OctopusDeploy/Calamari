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
        public string CustomPropertiesFile { get; set; }

        public static CommonOptions Parse(string[] args)
        {
            if (args.Length == 0)
                throw new CommandException("No command to run specified as a parameter");

            var command = args[0];
            if (string.IsNullOrWhiteSpace(command) || command.StartsWith("-"))
                throw new CommandException($"The command to run '{command}' is not a valid command name");

            var options = new CommonOptions(command);

            var set = new OptionSet()
                      .Add("variables=", "Path to a encrypted JSON file containing variables.", v => options.InputVariables.VariableFiles.Add(v))
                      .Add("variablesPassword=", "Password used to decrypt variables.", v => options.InputVariables.VariablesPassword = v)
                      .Add("outputVariables=", "Path to a encrypted JSON file containing output variables from previous executions.", v => options.InputVariables.OutputVariablesFile = v)
                      .Add("outputVariablesPassword=", "Password used to decrypt output variables", v => options.InputVariables.OutputVariablesPassword = v)
                      .Add("customProperties=", "Path to a encrypted JSON file containing custom properties.", v => options.CustomPropertiesFile = v);

            //these are legacy options to support the V2 pipeline
            set.Add("sensitiveVariables=", "(DEPRECATED) Path to a encrypted JSON file containing sensitive variables. This file format is deprecated.", v => options.InputVariables.DeprecatedFormatVariableFiles.Add(v))
               .Add("sensitiveVariablesPassword=", "(DEPRECATED) Password used to decrypt sensitive variables.", v => options.InputVariables.DeprecatedVariablesPassword = v);

            options.RemainingArguments = set.Parse(args.Skip(1));

            return options;
        }

        public class Variables
        {
            public List<string> VariableFiles { get; internal set; } = new List<string>();
            public string? VariablesPassword { get; internal set; }
            public string? OutputVariablesFile { get; internal set; }
            public string? OutputVariablesPassword { get; internal set; }

            //These are to support the V2 pipeline
            public List<string> DeprecatedFormatVariableFiles { get; internal set; } = new List<string>();
            public string? DeprecatedVariablesPassword { get; internal set; }
        }
    }
}