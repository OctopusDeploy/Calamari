using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    public interface ITemplateService
    {
        string GetSubstitutedTemplateContent(string relativePath, bool inPackage, IVariables variables);
    }
}
