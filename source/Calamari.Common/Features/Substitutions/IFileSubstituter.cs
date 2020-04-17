using Octostache;

namespace Calamari.Integration.Substitutions
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile, IVariables variables);
        void PerformSubstitution(string sourceFile, IVariables variables, string targetFile);
    }
}