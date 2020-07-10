namespace Calamari.Common.Features.StructuredVariables
{
    public interface IStructuredConfigVariableReplacer
    {
        void ModifyFile(string filePath, IVariables variables);
    }
}