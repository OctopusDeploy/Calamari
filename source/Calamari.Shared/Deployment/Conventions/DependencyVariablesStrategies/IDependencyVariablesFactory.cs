using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions.DependencyVariablesStrategies
{
    public interface IDependencyVariablesFactory
    {
        IDependencyVariables GetDependencyVariables(IVariables variables);
    }

    public interface IDependencyVariables
    {
        IEnumerable<string> GetIndexes();
        string OriginalPath(string referenceName);
        bool Extract(string referenceName);
        
        IDependencyOutputVariables OutputVariables { get; }
    }

    public interface IDependencyOutputVariables
    {
        string ExtractedPath(string referenceName);
        string FilePath(string referenceName);
        string FileName(string referenceName);
    }
}