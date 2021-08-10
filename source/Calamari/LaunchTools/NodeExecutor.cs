﻿using System;
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
                var jsonInputs = variables.GetRaw(instructions.InputsVariable) ?? string.Empty;
                variables.Set(instructions.InputsVariable, InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables, log));

                var variablesAsJson = variables.CloneAndEvaluate().SaveAsString();
                File.WriteAllBytes(variableFile.FilePath, new AesEncryption(options.InputVariables.SensitiveVariablesPassword).Encrypt(variablesAsJson));

                var commandLineInvocation = new CommandLineInvocation(BuildNodePath(pathToNode),
                                                                      BuildArgs(
                                                                                Path.Combine(pathToBootstrapper, "bootstrapper.js"),
                                                                                Path.Combine(pathToStepPackage, "executor.js"),
                                                                                variableFile.FilePath,
                                                                                options.InputVariables.SensitiveVariablesPassword,
                                                                                AesEncryption.SaltRaw,
                                                                                instructions.InputsVariable,
                                                                                instructions.DeploymentTargetInputsVariable))
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

        static string BuildArgs(string pathToBootstrapper, string pathToStepPackage, string pathToSensitiveVariables, string sensitiveVariablesSecret, string salt, string inputsKey, string deploymentTargetInputsKey) =>
            $"\"{pathToBootstrapper}\" \"{pathToStepPackage}\" \"{pathToSensitiveVariables}\" \"{sensitiveVariablesSecret}\" \"{salt}\" \"{inputsKey}\" \"{deploymentTargetInputsKey}\"";
    }

    public class NodeInstructions
    {
        public string NodePathVariable { get; set; }
        public string TargetPathVariable { get; set; }
        public string BootstrapperPathVariable { get; set; }
        public string InputsVariable { get; set; }
        
        public string DeploymentTargetInputsVariable { get; set; }
    }
}