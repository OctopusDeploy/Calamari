using System.Collections.Generic;
using System.IO;
using Calamari.Deployment.Conventions;

namespace Calamari.Commands.Support
{
    /// <summary>
    /// A command that requires the command line arguments passed to it. We are transitioning away from this interface  
    /// </summary>
    public interface ICommandWithArguments
    {
        int Execute(string[] commandLineArguments);
    }

    public interface ICommand
    {
        string PrimaryPackagePath { get; }
        IEnumerable<IConvention> GetConventions();
    }
}