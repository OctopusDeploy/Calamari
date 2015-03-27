using System;

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
                    commandOutput.WriteInfo,
                    commandOutput.WriteError);

                return new CommandResult(invocation.ToString(), exitCode, null);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Console.Error.WriteLine("The command that caused the exception was: " + invocation);
                return new CommandResult(invocation.ToString(), -1, ex.ToString());
            }
        }
    }
}