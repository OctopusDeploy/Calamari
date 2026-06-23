using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    static class VariableExtensions
    {
        // Selects which file the template and parameters are read from based on the template source.
        // Package and Git-repository sources both read files on disk (the mandatory ResourceGroupTemplate
        // variable, enforced by Server), whereas an inline source falls back to the conventional
        // template.json/parameters.json names.
        public static (string TemplateFile, string? TemplateParametersFile, bool FilesInPackageOrRepository) SelectTemplateInputs(this IVariables variables)
        {
            var filesInPackageOrRepository = variables.Get(SpecialVariables.Action.Azure.TemplateSource, string.Empty) is "Package" or "GitRepository";

            var templateFile = filesInPackageOrRepository
                ? variables.GetMandatoryVariable(SpecialVariables.Action.Azure.ResourceGroupTemplate)
                : variables.Get(SpecialVariables.Action.Azure.Template, "template.json");

            var templateParametersFile = filesInPackageOrRepository
                ? variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters)
                : variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");

            return (templateFile, templateParametersFile, filesInPackageOrRepository);
        }
    }
}
