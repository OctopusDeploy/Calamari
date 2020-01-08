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
            var timedOut = false;

            if (invocation.TimeoutMilliseconds > -1)
            {
                if (invocation.TimeoutMilliseconds == 0)
                {
                    var link = "https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.8#System_Diagnostics_Process_WaitForExit_System_Int32_";
                    commandOutput.WriteInfo($"The timeout for this script was set to 0. Perhaps this was not intended. Setting the timeout to 0 will succeed only if the script exits immediately. See {link}");
                }

                commandOutput.WriteInfo($"The script for this action will be executed with a timeout of {invocation.TimeoutMilliseconds} milliseconds. To remove this timeout, set the Action.Script.Timeout special variable to -1 or delete the variable.");
            }

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
                    invocation.TimeoutMilliseconds);

                timedOut = exitCode.TimedOut;

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
                    invocation.WorkingDirectory,
                    timedOut);
            }
        }

        public static string ConstructWin32ExceptionMessage(string executable)
        {
            return
                $"Unable to execute {executable}, please ensure that {executable} is installed and is in the PATH.{Environment.NewLine}";
        }
    }
}