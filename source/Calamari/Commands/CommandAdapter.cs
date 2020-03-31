using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;

namespace Calamari.Commands
{
    /// <summary>
    /// Adapts a ICommand to the ICommandWithArguments interface
    /// </summary>
    public class CommandAdapter
        : ICommandWithArguments
    {
        readonly ICommand command;
        readonly IVariables variables;

        public CommandAdapter(ICommand command, IVariables variables)
        {
            this.command = command;
            this.variables = variables;
        }

        public int Execute(string[] commandLineArguments)
        {
            var conventions = command.GetConventions().ToList();
            var deployment = new RunningDeployment(command.PrimaryPackagePath, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return variables.GetInt32(SpecialVariables.Action.Script.ExitCode) ?? 0;
        }
    }
}