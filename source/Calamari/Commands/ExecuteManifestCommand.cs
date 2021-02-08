using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Serialization;
using Calamari.Tools;
using Newtonsoft.Json;

namespace Calamari.Commands
{
    [Command("execute-manifest")]
    public class ExecuteManifestCommand : Command
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IEnumerable<Meta<IExecuteTool, ExecutionToolMeta>> executionTools;

        string executionManifestPath;

        public ExecuteManifestCommand(
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IEnumerable<Meta<IExecuteTool, ExecutionToolMeta>> executionTools)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.executionTools = executionTools;
            Options.Add("executionManifest=", "Path to a JSON file containing the execution manifest to execute", x => executionManifestPath = x);
        }

        // To solve: every handler can add to the variables collection. How do we ensure each handler gets an updated set to work with?

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(executionManifestPath, "No execution manifest path was supplied. Please pass --executionManifest \"path\\to\\manifest\\\"");

            var manifest = RetrieveExecutionManifest();

            foreach (var instruction in manifest.Instructions)
            {
                var tool = executionTools.First(x => x.Metadata.Tool == instruction.Tool);

                var result = tool.Value.Execute(instruction.Command, instruction.Inputs, commandLineArguments);

                if (result != 0) return result;
            }

            return 0;
        }

        public ExecutionManifest RetrieveExecutionManifest()
        {
            if (!fileSystem.FileExists(executionManifestPath))
                throw new CommandException($"Could not find execution manifest: {executionManifestPath}");

            var contents = fileSystem.ReadFile(executionManifestPath);

            return ExecutionManifest.FromFile(contents);
        }

        public class ExecutionManifest
        {
            public class Instruction
            {
                public ExecutionTool Tool { get; set; }
                public string Command { get; set; }
                public string Inputs { get; set; }
            }

            public ExecutionManifest(Instruction[] instructions)
            {
                if (instructions.Length == 0) throw new ArgumentException("The execution manifest must have at least one instruction", nameof(instructions));

                Instructions = instructions;
            }

            public Instruction[] Instructions { get; private set; }

            public static ExecutionManifest FromFile(string contents)
            {
                return JsonConvert.DeserializeObject<ExecutionManifest>(contents, JsonSerialization.GetDefaultSerializerSettings());
            }
        }
    }
}