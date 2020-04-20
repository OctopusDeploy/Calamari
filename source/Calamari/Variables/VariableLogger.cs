using System;
using System.Linq;
using System.Text;
using Calamari.Deployment;

namespace Calamari.Variables
{
    public class VariableLogger
    {
        readonly IVariables variables;
        readonly ILog log;

        public VariableLogger(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }
        
        public void LogVariables()
        {
            string ToString(bool useRawValue)
            {
                var text = new StringBuilder();

                var namesToPrint = variables.GetNames().Where(name => !name.Contains("CustomScripts.")).OrderBy(name => name);
                foreach (var name in namesToPrint)
                {
                    var value = useRawValue ? variables.GetRaw(name) : variables.Get(name);
                    text.AppendLine($"[{name}] = '{value}'");
                }

                return text.ToString();
            }

            if (variables.GetFlag(Common.Variables.SpecialVariables.PrintVariables))
            {
                log.Warn($"{Common.Variables.SpecialVariables.PrintVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                log.Verbose("The following variables are available:" + Environment.NewLine + ToString(true));
            }

            if (variables.GetFlag(Common.Variables.SpecialVariables.PrintEvaluatedVariables))
            {
                log.Warn($"{Common.Variables.SpecialVariables.PrintEvaluatedVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                log.Verbose("The following evaluated variables are available:" + Environment.NewLine + ToString(false));
            }
        }
    }
}