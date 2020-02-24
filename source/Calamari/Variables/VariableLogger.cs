using System;
using System.Linq;
using System.Text;
using Calamari.Deployment;

namespace Calamari.Variables
{
    static class VariableLogger
    {
        public static void LogVariables(IVariables variables)
        {
            string ToString(bool useRawValue)
            {
                var text = new StringBuilder();

                var namesToPrint = variables.GetNames().Where(name => name.Contains("CustomScripts.")).OrderBy(name => name);
                foreach (var name in namesToPrint)
                {
                    var value = useRawValue ? variables.GetRaw(name) : variables.Get(name);
                    text.AppendLine($"[{name}] = '{value}'");
                }

                return text.ToString();
            }

            if (variables.GetFlag(SpecialVariables.PrintVariables))
            {
                Log.Warn($"{SpecialVariables.PrintVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                Log.Verbose("The following variables are available:" + Environment.NewLine + ToString(true));
            }

            if (variables.GetFlag(SpecialVariables.PrintEvaluatedVariables))
            {
                Log.Warn($"{SpecialVariables.PrintEvaluatedVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                Log.Verbose("The following evaluated variables are available:" + Environment.NewLine + ToString(false));
            }
        }
    }
}