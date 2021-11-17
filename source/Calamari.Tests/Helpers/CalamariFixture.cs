using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Integration.Processes;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using NUnit.Framework;

namespace Calamari.Tests.Helpers
{
    public abstract class CalamariFixture
    {
        protected InMemoryLog Log;

        [SetUp]
        public void SetUpCalamariFixture()
        {
            Log = new InMemoryLog();
        }

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
            var args = command.GetRawArgs();
            var program = new TestProgram(Log);
            int exitCode;
            try
            {
                exitCode = program.RunWithArgs(args);
            }
            catch (Exception ex)
            {
                exitCode = ConsoleFormatter.PrintError(Log, ex);
            }

            variables = variables ?? new CalamariVariables();
            var capture = new CaptureCommandInvocationOutputSink();
            var sco = new SplitCommandInvocationOutputSink(new ServiceMessageCommandInvocationOutputSink(variables), capture);

            foreach(var line in Log.StandardOut)
                sco.WriteInfo(line);

            foreach(var line in Log.StandardError)
                sco.WriteError(line);

            return new CalamariResult(exitCode, capture);
        }

        protected CalamariResult Invoke(CommandLine command, IVariables variables = null)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, variables ?? new CalamariVariables());
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, runner.Output);
        }


        protected string GetFixtureResource(params string[] paths)
        {
            var type = GetType();
            return GetFixtureResource(type, paths);
        }

        public static string GetFixtureResource(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }

        protected (CalamariResult result, IVariables variables) RunScript(string scriptName,
            Dictionary<string, string> additionalVariables = null,
            Dictionary<string, string> additionalParameters = null,
            string sensitiveVariablesPassword = null,
            IEnumerable<string> extensions = null)
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new CalamariVariables();
            variables.Set(ScriptVariables.ScriptFileName, scriptName);
            variables.Set(ScriptVariables.ScriptBody, File.ReadAllText(GetFixtureResource("Scripts", scriptName)));
            variables.Set(ScriptVariables.Syntax, scriptName.ToScriptType().ToString());

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

                if (extensions != null)
                {
                    cmdBase.Argument("extensions", string.Join(",", extensions));
                }

                cmdBase = (additionalParameters ?? new Dictionary<string, string>()).Aggregate(cmdBase, (cmd, param) => cmd.Argument(param.Key, param.Value));

                var output = Invoke(cmdBase, variables);

                return (output, variables);

            }
        }
    }
}