using System.IO;
using Octostache;

namespace Calamari.Integration.Substitutions
{
    public class FileSubstituter
    {
        public void PerformSubstitution(string sourceFile, VariableDictionary variables)
        {
            PerformSubstitution(sourceFile, variables, sourceFile);
        }

        public void PerformSubstitution(string sourceFile, VariableDictionary variables, string targetFile)
        {
            var source = File.ReadAllText(sourceFile);

            var result = variables.Evaluate(source);
            File.WriteAllText(targetFile, result);
        }
    }
}
