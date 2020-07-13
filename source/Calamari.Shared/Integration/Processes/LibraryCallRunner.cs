using System;
using Calamari.Common.Features.Processes;

namespace Calamari.Integration.Processes
{
    public class LibraryCallRunner
    {
        public CommandResult Execute(LibraryCallInvocation invocation)
        {
            try
            {
                var exitCode = invocation.Executable(invocation.Arguments);

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