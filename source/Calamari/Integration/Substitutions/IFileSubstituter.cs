using Calamari.Extensibility;
using Octostache;

namespace Calamari.Integration.Substitutions
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile, IVariableDictionary variables);
        void PerformSubstitution(string sourceFile, IVariableDictionary variables, string targetFile);
    }
}