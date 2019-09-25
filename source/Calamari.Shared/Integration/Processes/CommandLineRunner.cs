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
                    commandOutput.WriteError);

                return new CommandResult(
                    invocation.ToString(),
                    exitCode.ExitCode,
                    exitCode.ErrorOutput,
                    invocation.WorkingDirectory);
            }       
            catch (Exception ex)
            {
                if (ex.InnerException is Win32Exception &&
                     string.Equals(ex.InnerException.Message,"The system cannot find the file specified", StringComparison.Ordinal))
                {
                    commandOutput.WriteError($"{invocation.Executable} was not found, please ensure that {invocation.Executable} is installed and is in the PATH");
                }
                else
                {
                    commandOutput.WriteError(ex.ToString());
                }
                                
                commandOutput.WriteError("The command that caused the exception was: " + invocation);

                return new CommandResult(
                    invocation.ToString(), 
                    -1, 
                    ex.ToString(),
                    invocation.WorkingDirectory);
            }
        }
    }
}