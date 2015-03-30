using System;
using System.Text;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Integration.Processes
{
    public static class VariableDictionaryExtensions
    {
        public static void EnrichWithEnvironmentVariables(this VariableDictionary variables)
        {
            var environmentVariables = Environment.GetEnvironmentVariables();

            foreach (var name in environmentVariables.Keys)
            {
                variables["env:" + name] = (environmentVariables[name] ?? string.Empty).ToString();
            }
        }

        /// <summary>
        /// Logs raw and evaluated variables, if the corresponding flags are set
        /// </summary>
        public static void LogVariables(this VariableDictionary variables)
        {
            if (variables.GetFlag(SpecialVariables.PrintVariables))
            {
                Log.Verbose("The following variables are available:" + Environment.NewLine + variables.ToString(IsPrintable, true));
            }

            if (variables.GetFlag(SpecialVariables.PrintEvaluatedVariables))
            {
                Log.Verbose("The following evaluated variables are available:" + Environment.NewLine + variables.ToString(IsPrintable, false));
            }
        }

        private static string ToString(this VariableDictionary variables, Func<string, bool> nameFilter, bool useRawValue)
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

        private static bool IsPrintable(string variableName)
        {
            return !variableName.Contains("CustomScripts.");
        }
    }
}