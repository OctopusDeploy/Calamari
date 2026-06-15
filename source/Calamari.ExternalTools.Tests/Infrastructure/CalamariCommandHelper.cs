using System;
using System.Reflection;
using Autofac;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Runs Calamari commands in-process for testing.
    /// Slim equivalent of CalamariFixture from Calamari.Tests.
    /// </summary>
    public static class CalamariCommandHelper
    {
        public static CommandLine CreateCommand()
        {
            var folder = System.IO.Path.GetDirectoryName(typeof(Calamari.Program).Assembly.FullLocalPath());
            var calamariFullPath = System.IO.Path.Combine(folder!, "Calamari.ExternalTools.Tests.dll");

            return new CommandLine(calamariFullPath).UseDotnet().OutputToLog(false);
        }

        public static CalamariResult InvokeInProcess(CommandLine command, InMemoryLog log)
        {
            var args = command.GetRawArgs();
            var program = new TestableProgram(log);
            int exitCode;
            try
            {
                exitCode = program.RunWithArgs(args);
            }
            catch (Exception ex)
            {
                exitCode = ConsoleFormatter.PrintError(log, ex);
            }

            IVariables variables = new CalamariVariables();
            var capture = new CaptureCommandInvocationOutputSink();
            var sco = new SplitCommandInvocationOutputSink(
                new ServiceMessageCommandInvocationOutputSink(variables), capture);

            foreach (var line in log.StandardOut)
                sco.WriteInfo(line);

            foreach (var line in log.StandardError)
                sco.WriteError(line);

            return new CalamariResult(exitCode, capture);
        }

        class TestableProgram : Calamari.Program
        {
            public TestableProgram(ILog log) : base(log) { }

            public int RunWithArgs(string[] args) => Run(args);

            protected override Assembly GetProgramAssemblyToRegister()
                => typeof(Calamari.Program).Assembly;

            protected override System.Collections.Generic.IEnumerable<Assembly> GetAllAssembliesToRegister()
            {
                foreach (var assembly in base.GetAllAssembliesToRegister())
                    yield return assembly;
                yield return typeof(TestableProgram).Assembly;
            }
        }
    }
}
