using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Calamari.Integration.ServiceMessages;
using Calamari.Util;

namespace Calamari.Integration.Processes
{
    public class CommandLineRunner : ICommandLineRunner
    {
        readonly IVariables variables;

        public CommandLineRunner(IVariables variables)
        {
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
                    commandOutput.WriteError);

                return new CommandResult(
                    invocation.ToString(),
                    exitCode.ExitCode,
                    exitCode.ErrorOutput,
                    invocation.WorkingDirectory);
            }       
            catch (Exception ex)
            {
                if (ex.InnerException is Win32Exception )
                {
                    commandOutput.WriteError(ConstructWin32ExceptionMessage(invocation.Executable));
                }
                
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

                outputs.Add(new LogCommandInvocationOutputSink(invocation.OutputAsVerbose));
            if (invocation.OutputToLog)

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