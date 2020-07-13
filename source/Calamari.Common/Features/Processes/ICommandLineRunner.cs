using System;

namespace Calamari.Common.Features.Processes
{
    public interface ICommandLineRunner
    {
        CommandResult Execute(CommandLineInvocation invocation);
    }
}