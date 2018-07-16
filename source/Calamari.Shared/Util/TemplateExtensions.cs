using Calamari.Integration.Processes;

namespace Calamari.Util
{
    public static class TemplateExtensions
    {
        public static string ApplyVariableSubstitution(this ITemplate template, CalamariVariableDictionary variables)
        {
            return variables.Evaluate(template.Content);
        }
    }
}