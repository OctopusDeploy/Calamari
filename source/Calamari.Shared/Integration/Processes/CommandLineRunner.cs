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
                    ex.InnerException.Message == "The system cannot find the file specified")
                {
                    Console.Error.WriteLine($"{invocation.Executable} was not found, please ensure that this executable is a supported - https://octopus.com/docs/deployment-examples/custom-scripts and is installed");
                }
                else
                {
                    Console.Error.WriteLine(ex);
                }
                                
                Console.Error.WriteLine("The command that caused the exception was: " + invocation);

                return new CommandResult(
                    invocation.ToString(), 
                    -1, 
                    ex.ToString(),
                    invocation.WorkingDirectory);
            }
        }
    }
}