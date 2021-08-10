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
            var template = TemplateParser.ParseTemplate(jsonInputs);
            jsonInputs = template.ToString(); // we parse the template back to string to have a consistent representation of Octostache expressions
            foreach (var templateToken in template.Tokens)
            {
                var arguments = templateToken.GetArguments();
                if (!arguments.Any()) continue; // to avoid TextToken, which doesn't need to be evaluated
                string evaluated = variables.Evaluate(templateToken.ToString());
                if (evaluated == templateToken.ToString())
                    log.Warn($"Expression {templateToken.ToString()} could not be evaluated, check that the referenced variables exist.");
                jsonInputs = jsonInputs.Replace($"\"{templateToken.ToString()}\"", JsonConvert.ToString(evaluated));
            }

            return jsonInputs;
        }
    }
}