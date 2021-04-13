using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
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

            using (var variableFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var variablesAsJson = variables.CloneAndEvaluate().SaveAsString();
                File.WriteAllBytes(variableFile.FilePath, new AesEncryption(options.InputVariables.SensitiveVariablesPassword).Encrypt(variablesAsJson));
                var commandLineInvocation = new CommandLineInvocation(BuildNodePath(pathToNode),
                                                                      BuildArgs(
                                                                                Path.Combine(pathToBootstrapper, "bootstrapper.js"),
                                                                                Path.Combine(pathToStepPackage, instructions.TargetEntryPoint),
                                                                                variableFile.FilePath,
                                                                                options.InputVariables.SensitiveVariablesPassword,
                                                                                "Octopuss",
                                                                                instructions.InputsVariable))
                {
                    WorkingDirectory = runningDeployment.CurrentDirectory,
                    OutputToLog = true,
                };

                var commandResult = commandLineRunner.Execute(commandLineInvocation);

                return commandResult.ExitCode;
            }
        }

        static string BuildNodePath(string pathToNode) => CalamariEnvironment.IsRunningOnWindows ? Path.Combine(pathToNode, "node.exe") : Path.Combine(pathToNode, "bin", "node");

        static string BuildArgs(string pathToBootstrapper, string pathToStepPackage, string pathToSensitiveVariables, string sensitiveVariablesSecret, string salt, string inputsKey) =>
            $"\"{pathToBootstrapper}\" \"{pathToStepPackage}\" \"{pathToSensitiveVariables}\" \"{sensitiveVariablesSecret}\" \"{salt}\" \"{inputsKey}\"";
    }

    public class NodeInstructions
    {
        public string NodePathVariable { get; set; }
        public string TargetPathVariable { get; set; }
        public string BootstrapperPathVariable { get; set; }
        public string TargetEntryPoint { get; set; }
        public string InputsVariable { get; }
    }
}