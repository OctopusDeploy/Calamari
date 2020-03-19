using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;
using Autofac;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Variables;

namespace Calamari.Tests.Helpers
{
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
#if NETFX
            var calamariFullPath = typeof(DeployPackageCommand).Assembly.FullLocalPath();
            return new CommandLine(calamariFullPath);

#else
            var folder = Path.GetDirectoryName(typeof(Program).Assembly.FullLocalPath());
            var calamariFullPath = Path.Combine(folder, "Calamari.Tests.dll");

            if (!File.Exists(calamariFullPath))
                throw new Exception($"Could not find Calamari test wrapper at {calamariFullPath}");
            return new CommandLine(calamariFullPath).UseDotnet();
#endif
        }

        protected CommandLine OctoDiff()
        {
            var octoDiffExe = OctoDiffCommandLineRunner.FindOctoDiffExecutable();

            return new CommandLine(octoDiffExe);
        }

        protected CalamariResult InvokeInProcess(CommandLine command, IVariables variables = null)
        {
            using (var logs = new ProxyLog())
            {
                var args = command.GetRawArgs();
                var exitCode = Program.Main(args);

                var capture = new CaptureCommandOutput();
                var sco = new SplitCommandOutput(
                    new ConsoleCommandOutput(false), 
                    new ServiceMessageCommandOutput(variables ?? new CalamariVariables()),
                    capture);
                logs.Flush(sco);
                return new CalamariResult(exitCode, capture);
            }
        }

        protected CalamariResult Invoke(CommandLine command, IVariables variables = null)
        {
            var runner = new TestCommandLineRunner(variables ?? new CalamariVariables());
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, runner.Output);
        }


        protected string GetFixtureResouce(params string[] paths)
        {
            var type = GetType();
            return GetFixtureResouce(type, paths);
        }

        public static string GetFixtureResouce(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }

        protected (CalamariResult result, IVariables variables) RunScript(string scriptName,
            Dictionary<string, string> additionalVariables = null,
            Dictionary<string, string> additionalParameters = null,
            string sensitiveVariablesPassword = null)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Action.Script.ScriptFileName, scriptName);
            variables.Set(SpecialVariables.Action.Script.ScriptBody, File.ReadAllText(GetFixtureResouce("Scripts", scriptName)));
            variables.Set(SpecialVariables.Action.Script.Syntax, scriptName.ToScriptType().ToString());
            
            additionalVariables?.ToList().ForEach(v => variables[v.Key] = v.Value);

            using (new TemporaryFile(variablesFile))
            {
                var cmdBase = Calamari()
                    .Action("run-script");

                if (sensitiveVariablesPassword == null)
                {
                    variables.Save(variablesFile);
                    cmdBase = cmdBase.Argument("variables", variablesFile);
                }
                else
                {
                    variables.SaveEncrypted(sensitiveVariablesPassword, variablesFile);
                    cmdBase = cmdBase.Argument("sensitiveVariables", variablesFile)
                        .Argument("sensitiveVariablesPassword", sensitiveVariablesPassword);
                }

                cmdBase = (additionalParameters ?? new Dictionary<string, string>()).Aggregate(cmdBase, (cmd, param) => cmd.Argument(param.Key, param.Value));

                var output = Invoke(cmdBase, variables);

                return (output, variables);

            }
        }
    }
}