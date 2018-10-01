using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Commands.Support;

namespace Calamari.Modules
{
    public class VariableDictionaryUtils : IVariableDictionaryUtils
    {
        public void PopulateOptions(
            OptionSet optionSet,
            Action<string> variablesFile,
            Action<string> outputVariablesFile,
            Action<string> outputVariablesPassword,
            Action<string> sensitiveVariablesFile,
            Action<string> sensitiveVariablesPassword)
        {
            optionSet.Add("variables=", "Path to a JSON file containing variables.", variablesFile);
            optionSet.Add("outputVariables=", "Base64 encoded encrypted JSON file containing output variables.", outputVariablesFile);
            optionSet.Add("outputVariablesPassword=", "Password used to decrypt output-variables", outputVariablesPassword);
            optionSet.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", sensitiveVariablesFile);
            optionSet.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", sensitiveVariablesPassword);
        }

        public void PopulateOptions(OptionSet optionSet)
        {
            optionSet.Add("variables=", "Path to a JSON file containing variables.", v => { });
            optionSet.Add("outputVariables=", "Base64 encoded encrypted JSON file containing output variables.", v => { });
            optionSet.Add("outputVariablesPassword=", "Password used to decrypt output-variables", v => { });
            optionSet.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => { });
            optionSet.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => { });
        }
    }
}
