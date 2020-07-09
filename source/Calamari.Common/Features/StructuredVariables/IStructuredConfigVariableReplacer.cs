using Octostache;

namespace Calamari.Features.StructuredVariables
{
    public interface IStructuredConfigVariableReplacer
    {
        void ModifyFile(string filePath, IVariables variables);
    }
}