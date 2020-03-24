using Calamari.Commands.Support;

namespace Calamari.Commands
{
    /// <summary>
    /// Adapts a ICommand to the ICommandWithArguments interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandAdapter<T> 
        : ICommandWithArguments
        where T : ICommand
    {
        readonly ICommand command;

        public CommandAdapter(ICommand command)
        {
            this.command = command;
        }

        public int Execute(string[] commandLineArguments)
            => command.Execute();
    }
}