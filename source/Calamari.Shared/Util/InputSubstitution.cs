using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Octostache;
using Octostache.Templates;

namespace Calamari.Util
{
    public static class InputSubstitution
    {
        public static string SubstituteAndEscapeAllVariablesInJson(string jsonInputs, IVariables variables)
        {
            var tempVariableDictionaryToUseForExpandedVariables = new VariableDictionary();
            var template = TemplateParser.ParseTemplate(jsonInputs);
            foreach (var templateToken in template.Tokens)
            {
                // TODO: we need change this to have a better way of escaping json string here
                var arguments = templateToken.GetArguments();
                if (!arguments.Any()) continue; // to avoid TextToken, which doesn't need to be evaluated
                var variableName = templateToken.ToString()
                                                .Replace("#{", string.Empty)
                                                .Replace("}", string.Empty);
                var expanded = variables.Evaluate($"#{{{variableName} | JsonEscape}}");
                tempVariableDictionaryToUseForExpandedVariables.Add(variableName, expanded);
            }

            var evaluatedJson = tempVariableDictionaryToUseForExpandedVariables.Evaluate(jsonInputs);
            return evaluatedJson;
        }
    }
}