using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Commands.Support;

namespace Calamari.Common
{
    public class CommonOptions
    {

        internal CommonOptions(string command)
        {
            Command = command;
        }

        public string Command { get; }
        public Variables InputVariables { get; } = new Variables();

        public static CommonOptions Parse(string[] args)
        {
            if (args.Length == 0)
                throw new CommandException("No command to run specified as a parameter");

            var command = args[0];
            if (string.IsNullOrWhiteSpace(command) || command.StartsWith("-"))
                throw new CommandException($"The command to run '{command}' is not a valid command name");

            var options = new CommonOptions(command);

            var parameters = new string[args.Length - 1];
            args.CopyTo(parameters, 1);

            options.InputVariables.VariablesFile = ParseArgument("variables", parameters);
            options.InputVariables.OutputVariablesFile = ParseArgument("outputVariables", parameters);
            options.InputVariables.OutputVariablesPassword = ParseArgument("outputVariablesPassword", parameters);
            options.InputVariables.SensitiveVariablesFiles.AddRange(ParseArguments("sensitiveVariables", parameters));
            options.InputVariables.SensitiveVariablesPassword = ParseArgument("sensitiveVariablesPassword", parameters);

            return options;
        }

        static string ParseArgument(string argumentName, string[] args)
        {
            var formattedArgumentName = $"-${argumentName}";
            var argument = args.FirstOrDefault(x => x.StartsWith(formattedArgumentName));
            if(argument == null)
            {
                return string.Empty;
            }

            var index = Array.IndexOf(args, formattedArgumentName);

            return GetArgumentValue(argumentName, args, index);
        }

        static string GetArgumentValue(string argumentName, string[] args, int index)
        {
            if (args.Length - 1 == index || args[index + 1].StartsWith("-"))
            {
                throw new ApplicationException(
                    $"Argument {argumentName} value not provided. Please use two double quotes to denote an empty string");
            }
            
            return args[index + 1];
        }

        static IEnumerable<string> ParseArguments(string argumentName, string[] args)
        {
            var arguments = new List<string>();
            var formattedArgumentName = $"-${argumentName}";
            
            for (var i = 0; i < args.Length; i += 2)
            {
                if (args[i].StartsWith(formattedArgumentName))
                {
                    var value = GetArgumentValue(argumentName, args, i);
                    arguments.Add(value);
                }
            }

            return arguments;
        }

        public class Variables
        {
            public string VariablesFile { get; internal set; }
            public List<string> SensitiveVariablesFiles { get; } = new List<string>();
            public string SensitiveVariablesPassword { get; internal set; }
            public string OutputVariablesFile { get; internal set; }
            public string OutputVariablesPassword { get; internal set; }
        }
    }
}