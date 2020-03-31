using System.Collections.Generic;
using Calamari.Deployment.Conventions;

namespace Calamari.Commands.Support
{
    public interface ICommand
    {
        string PrimaryPackagePath { get; }
        IEnumerable<IConvention> GetConventions();
    }
}