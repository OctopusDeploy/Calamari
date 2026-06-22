using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    interface ITemplateService
    {
        string GetSubstitutedTemplateContent(string relativePath, bool inPackage, IVariables variables);
    }
}
