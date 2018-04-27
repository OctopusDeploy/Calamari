using Calamari.Commands.Support;


namespace Calamari.Commands
{
    [Command("noop", Description = "No op command that returns an error")]
    class NoOpCommand : Command
    {

        public override int Execute(string[] commandLineArguments)
        {
            return 1;
        }
    }
}
