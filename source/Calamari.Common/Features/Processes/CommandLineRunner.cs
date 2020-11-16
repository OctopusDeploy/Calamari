using System;
using System.Collections.Generic;
using System.ComponentModel;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Processes
{
    public class CommandLineRunner : ICommandLineRunner
    {
        readonly ILog log;
        readonly IVariables variables;

        public CommandLineRunner(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }

        public CommandResult Execute(CommandLineInvocation invocation)
        {
            var commandOutput = new SplitCommandInvocationOutputSink(GetCommandOutputs(invocation));

            try
            {
                var exitCode = SilentProcessRunner.ExecuteCommand(
                    invocation.Executable,
                    invocation.Arguments,
                    invocation.WorkingDirectory,
                    invocation.EnvironmentVars,
                    invocation.UserName,
                    invocation.Password,
                    commandOutput.WriteInfo,
                    commandOutput.WriteError,
                    invocation.Timeout);

                return new CommandResult(
                    invocation.ToString(),
                    exitCode.ExitCode,
                    exitCode.ErrorOutput,
                    invocation.WorkingDirectory,
                    exitCode.TimedOut);
            }
            catch (Exception ex)
            {
                if (ex.InnerException is Win32Exception)
                    commandOutput.WriteError(ConstructWin32ExceptionMessage(invocation.Executable));

                commandOutput.WriteError(ex.ToString());
                commandOutput.WriteError("The command that caused the exception was: " + invocation);

                return new CommandResult(
                    invocation.ToString(),
                    -1,
                    ex.ToString(),
                    invocation.WorkingDirectory);
            }
        }

        protected virtual List<ICommandInvocationOutputSink> GetCommandOutputs(CommandLineInvocation invocation)
        {
            var outputs = new List<ICommandInvocationOutputSink>
            {
                new ServiceMessageCommandInvocationOutputSink(variables)
            };

            if (invocation.OutputToLog)
                outputs.Add(new LogCommandInvocationOutputSink(log, invocation.OutputAsVerbose));

            if (invocation.AdditionalInvocationOutputSink != null)
                outputs.Add(invocation.AdditionalInvocationOutputSink);

            return outputs;
        }

        public static string ConstructWin32ExceptionMessage(string executable)
        {
            return
                $"Unable to execute {executable}, please ensure that {executable} is installed and is in the PATH.{Environment.NewLine}";
        }
    }
}