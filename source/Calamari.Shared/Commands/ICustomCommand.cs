namespace Calamari.Shared.Commands
{
    public interface ICustomCommand
    {
        //IOptionsBuilder Options(IOptionsBuilder optionsBuilder);
        ICommandBuilder Run(ICommandBuilder commandBuilder);
    }
}