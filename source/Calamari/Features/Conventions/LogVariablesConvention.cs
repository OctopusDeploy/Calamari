using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Shared;
using Calamari.Shared.Convention;

namespace Calamari.Features.Conventions
{
    [ConventionMetadata(CommonConventions.LogVariables, "Logs raw and evaluated variables")]
    public class LogVariablesConvention : IInstallConvention
    {
        private readonly Shared.ILog log;

        public LogVariablesConvention(Shared.ILog log)
        {
            this.log = log;
        }

        public void Install(IVariableDictionary variables)
        {
            if (variables.GetFlag(SpecialVariables.PrintVariables))
            {
                log.Warn($"{SpecialVariables.PrintVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                log.Verbose("The following variables are available:" + Environment.NewLine + VariablesToString(variables, IsPrintable, true));
            }

            if (variables.GetFlag(SpecialVariables.PrintEvaluatedVariables))
            {
                log.Warn($"{SpecialVariables.PrintEvaluatedVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
                log.Verbose("The following evaluated variables are available:" + Environment.NewLine + VariablesToString(variables, IsPrintable, false));
            }
        }

        private static bool IsPrintable(string variableName)
        {
            return !variableName.Contains("CustomScripts.");
        }

        private static string VariablesToString(IVariableDictionary variables, Func<string, bool> nameFilter, bool useRawValue)
        {
            var text = new StringBuilder();

            foreach (var name in variables.GetNames())
            {
                if (!nameFilter(name))
                    continue;

                text.AppendFormat("[{0}] = '{1}'", name, useRawValue ? variables.GetRaw(name) : variables.Get(name));
                text.AppendLine();
            }

            return text.ToString();
        }
    }

}
