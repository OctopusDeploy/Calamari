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
            Action<string> base64Variables,
            Action<string> sensitiveVariablesFile,
            Action<string> sensitiveVariablesPassword)
        {
            optionSet.Add("variables=", "Path to a JSON file containing variables.", variablesFile);
            optionSet.Add("base64Variables=", "JSON string containing variables.", base64Variables);
            optionSet.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", sensitiveVariablesFile);
            optionSet.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", sensitiveVariablesPassword);
        }

        public void PopulateOptions(OptionSet optionSet)
        {
            optionSet.Add("variables=", "Path to a JSON file containing variables.", v => { });
            optionSet.Add("base64Variables=", "JSON string containing variables.", v => { });
            optionSet.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => { });
            optionSet.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => { });
        }
    }
}
