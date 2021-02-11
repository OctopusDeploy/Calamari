using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Features.Metadata;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.LaunchTools;

namespace Calamari.Commands
{
    [Command("execute-manifest")]
    public class ExecuteManifestCommand : Command
    {
        readonly IVariables variables;
        readonly IExecutionManifest executionManifest;
        readonly IEnumerable<Meta<ILaunchTool, LaunchToolMeta>> executionTools;

        string executionManifestPath;

        public ExecuteManifestCommand(
            IVariables variables,
            IExecutionManifest executionManifest,
            IEnumerable<Meta<ILaunchTool, LaunchToolMeta>> executionTools)
        {
            this.variables = variables;
            this.executionManifest = executionManifest;
            this.executionTools = executionTools;
            Options.Add("executionManifest=", "Path to a JSON file containing the execution manifest to execute", x => executionManifestPath = x);
        }

        // To solve: every handler can add to the variables collection. How do we ensure each handler gets an updated set to work with?

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(executionManifestPath, "No execution manifest path was supplied. Please pass --executionManifest \"path\\to\\manifest\\\"");

            var instructions = executionManifest.Create(executionManifestPath);

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