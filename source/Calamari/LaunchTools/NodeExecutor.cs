using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Octostache;
using Octostache.Templates;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Node)]
    public class NodeExecutor : LaunchTool<NodeInstructions>
    {
        readonly CommonOptions options;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;

        public NodeExecutor(CommonOptions options, IVariables variables, ICommandLineRunner commandLineRunner)
        {
            this.options = options;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
        }

        protected override int ExecuteInternal(NodeInstructions instructions, params string[] args)
        {
            Thread.Sleep(20000);
            var pathToNode = variables.Get(instructions.NodePathVariable);
            var pathToStepPackage = variables.Get(instructions.TargetPathVariable);
            var pathToBootstrapper = variables.Get(instructions.BootstrapperPathVariable);
            var runningDeployment = new RunningDeployment(variables);

            using (var variableFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Set(instructions.InputsVariable, JsonEscapeAllVariablesInOurInputs(instructions));

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

        string JsonEscapeAllVariablesInOurInputs(NodeInstructions instructions)
        {
            var rawJson = variables.GetRaw(instructions.InputsVariable);
            var tempVariableDictionaryToUseForExpandedVariables = new VariableDictionary();
            var template = TemplateParser.ParseTemplate(rawJson);
            foreach (var templateToken in template.Tokens)
            {
                var variableName = templateToken.ToString()
                                                .Replace("#{", string.Empty)
                                                .Replace("}", string.Empty);
                var expanded = variables.Evaluate($"#{{{variableName} | JsonEscape}}");
                tempVariableDictionaryToUseForExpandedVariables.Add(variableName, expanded);
            }

            var evaluatedJson = tempVariableDictionaryToUseForExpandedVariables.Evaluate(rawJson);
            return evaluatedJson;
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