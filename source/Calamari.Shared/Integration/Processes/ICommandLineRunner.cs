namespace Calamari.Integration.Processes
{
    public interface ICommandLineRunner
    {
        CommandResult Execute(CommandLineInvocation invocation);
    }
}
