using System;
using System.ComponentModel;

namespace Calamari.Integration.Processes
{
    public class CommandLineRunner : ICommandLineRunner
    {
        private readonly ICommandOutput commandOutput;

        public CommandLineRunner(ICommandOutput commandOutput)
        {
            this.commandOutput = commandOutput;
        }

        public CommandResult Execute(CommandLineInvocation invocation)
        {
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
                    invocation.WorkingDirectory,
                    false);
            }
        }

        public static string ConstructWin32ExceptionMessage(string executable)
        {
            return
                $"Unable to execute {executable}, please ensure that {executable} is installed and is in the PATH.{Environment.NewLine}";
        }
    }
}