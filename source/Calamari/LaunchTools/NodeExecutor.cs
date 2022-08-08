using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Util;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Node)]
    public class NodeExecutor : LaunchTool<NodeInstructions>
    {
        readonly CommonOptions options;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        const string DebugVariableName = "Octopus.StepPackage.Bootstrap.Debug";

        public NodeExecutor(CommonOptions options, IVariables variables, ICommandLineRunner commandLineRunner, ILog log)
        {
            this.options = options;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
        }

        protected override int ExecuteInternal(NodeInstructions instructions)
        {
            using (var variableFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var jsonInputs = variables.GetRaw(instructions.InputsVariable) ?? string.Empty;
                variables.Set(instructions.InputsVariable, InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables, log));
                var variablesAsJson = variables.CloneAndEvaluate().SaveAsString();
                File.WriteAllBytes(variableFile.FilePath, new AesEncryption(options.InputVariables.SensitiveVariablesPassword).Encrypt(variablesAsJson));
                var pathToNode = variables.Get(instructions.NodePathVariable);
                var nodeExecutablePath = BuildNodePath(pathToNode);
                var parameters = BuildParams(instructions, variableFile.FilePath);
                var runningDeployment = new RunningDeployment(variables);
                var commandLineInvocation = new CommandLineInvocation(nodeExecutablePath, parameters)
                {
                    WorkingDirectory = runningDeployment.CurrentDirectory,
                    OutputToLog = true,
                    EnvironmentVars = ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables().ToDictionary(e => e.Key, e => e.Value)
                };

                var commandResult = commandLineRunner.Execute(commandLineInvocation);
                return commandResult.ExitCode;
            }
        }

        static string BuildNodePath(string pathToNode) => CalamariEnvironment.IsRunningOnWindows ? Path.Combine(pathToNode, "node.exe") : Path.Combine(pathToNode, "bin", "node");

        string BuildParams(NodeInstructions instructions, string sensitiveVariablesFilePath)
        {
            var parameters = new List<string>();
            var debugMode = variables.GetFlag(DebugVariableName, false);
            if (debugMode)
            {
                parameters.Add("--inspect-brk");
            }

            var pathToBootstrapperFolder = variables.Get(instructions.BootstrapperPathVariable);
            var pathToBootstrapper = Path.Combine(pathToBootstrapperFolder, "bootstrapper.js");
            parameters.Add(pathToBootstrapper);
            parameters.Add(instructions.BootstrapperInvocationCommand);
            var pathToStepPackage = variables.Get(instructions.TargetPathVariable);
            parameters.Add(pathToStepPackage);
            parameters.Add(sensitiveVariablesFilePath);
            parameters.Add(options.InputVariables.SensitiveVariablesPassword);
            parameters.Add(AesEncryption.SaltRaw);
            if (string.Equals(instructions.BootstrapperInvocationCommand, "Execute", StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add(instructions.InputsVariable);
                parameters.Add(instructions.DeploymentTargetInputsVariable);
            }
            else if (string.Equals(instructions.BootstrapperInvocationCommand, "Discover", StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add(instructions.TargetDiscoveryContextVariable);
            }
            else
            {
                throw new CommandException($"Unknown bootstrapper invocation command: '{instructions.BootstrapperInvocationCommand}'");
            }

            return string.Join(" ", parameters.Select(p => $"\"{p}\""));
        }
    }

    public class NodeInstructions
    {
        public string NodePathVariable { get; set; }
        public string TargetPathVariable { get; set; }
        public string BootstrapperPathVariable { get; set; }
        public string BootstrapperInvocationCommand { get; set; }
        public string InputsVariable { get; set; }
        public string DeploymentTargetInputsVariable { get; set; }
        public string TargetDiscoveryContextVariable { get; set; }
    }
}