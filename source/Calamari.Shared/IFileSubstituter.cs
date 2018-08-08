using Octostache;

namespace Calamari.Shared
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile, VariableDictionary variables);
        void PerformSubstitution(string sourceFile, VariableDictionary variables, string targetFile);
    }
}