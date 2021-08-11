using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octostache;
using Octostache.Templates;

namespace Calamari.Util
{
    public static class InputSubstitution
    {
        public static string SubstituteAndEscapeAllVariablesInJson(string jsonInputs, IVariables variables, ILog log)
        {
            if (!TemplateParser.TryParseTemplate(jsonInputs, out var template, out string error))
            {
                throw new CommandException($"Variable expression could not be parsed. Error: {error}");
            }
            
            jsonInputs = template.ToString(); // we parse the template back to string to have a consistent representation of Octostache expressions
            foreach (var templateToken in template.Tokens)
            {
                string evaluated = variables.Evaluate(templateToken.ToString());
                if (templateToken.GetArguments().Any() && evaluated == templateToken.ToString())
                    log.Warn($"Expression {templateToken.ToString()} could not be evaluated, check that the referenced variables exist.");
                jsonInputs = jsonInputs.Replace($"\"{templateToken.ToString()}\"", JsonConvert.ToString(evaluated));
            }

            return jsonInputs;
        }
    }
}