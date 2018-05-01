using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Commands.Support;

namespace Calamari.Modules
{
    public interface IVariableDictionaryUtils
    {
        /// <summary>
        /// Populate an option set with the arguments required to build a calamari variable dictionary
        /// </summary>
        /// <param name="optionSet"></param>
        /// <param name="variablesFile">Callback to set the variables file</param>
        /// <param name="base64Variables">Callback to set the base 64 variables</param>
        /// <param name="sensitiveVariablesFile">Callback to set the sensitive variables file</param>
        /// <param name="sensitiveVariablesPassword">Callback to set the sensitive variables password</param>
        void PopulateOptions(OptionSet optionSet,
            Action<string> variablesFile,
            Action<string> base64Variables,
            Action<string> sensitiveVariablesFile,
            Action<string> sensitiveVariablesPassword);

        /// <summary>
        /// Populate an option set with the arguments required to build a calamari variable dictionary,
        /// but with no actions associated with the variables. This is basically just to allow the help text
        /// to work as expected.
        /// </summary>
        /// <param name="optionSet"></param>
        void PopulateOptions(OptionSet optionSet);
    }
}
