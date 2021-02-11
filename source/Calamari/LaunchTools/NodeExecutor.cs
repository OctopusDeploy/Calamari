using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Node)]
    public class NodeExecutor : LaunchTool<NodeInstructions>
    {
        readonly CommonOptions options;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        public NodeExecutor(CommonOptions options, IVariables variables, ICommandLineRunner commandLineRunner, ILog log)
        {
            this.options = options;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
        }

        protected override int ExecuteInternal(NodeInstructions instructions, params string[] args)
        {
            var pathToNode = variables.Get(instructions.NodePathVariable);
            var pathToStepPackage = variables.Get(instructions.TargetPathVariable);
            var pathToBootstrapper = variables.Get(instructions.BootstrapperPathVariable);
            var runningDeployment = new RunningDeployment(variables);

            var commandLineInvocation = new CommandLineInvocation(BuildNodePath(pathToNode),
                                                                  BuildArgs(Path.Combine(pathToBootstrapper, "bootstrapper.js"), Path.Combine(pathToStepPackage, instructions.TargetEntryPoint), options.InputVariables.SensitiveVariablesFiles.First(), options.InputVariables.SensitiveVariablesPassword))
            {
                WorkingDirectory = runningDeployment.CurrentDirectory,
                OutputToLog = true,
            };

            log.Info(commandLineInvocation.ToString());

            var commandResult = commandLineRunner.Execute(commandLineInvocation);

            return commandResult.ExitCode;
        }

        static string BuildNodePath(string pathToNode) => CalamariEnvironment.IsRunningOnWindows ? Path.Combine(pathToNode, "node.exe") : Path.Combine(pathToNode, "bin", "node");

        static string BuildArgs(string pathToBootstrapper, string pathToStepPackage, string pathToSensitiveVariables, string sensitiveVariablesSecret) =>
            $"\"{pathToBootstrapper}\" \"{pathToStepPackage}\" \"{pathToSensitiveVariables}\" {sensitiveVariablesSecret}";
    }

    public class NodeInstructions
    {
        public string NodePathVariable { get; set; }
        public string TargetPathVariable { get; set; }
        public string BootstrapperPathVariable { get; set; }
        public string TargetEntryPoint { get; set; }
    }
}