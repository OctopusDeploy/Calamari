using Octostache;

namespace Calamari.Integration.Substitutions
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile, VariableDictionary variables);
        void PerformSubstitution(string sourceFile, VariableDictionary variables, string targetFile);
    }
}