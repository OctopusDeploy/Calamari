using Calamari.Commands.Support;
using System;

namespace Calamari.Modules
{
    /// <summary>
    /// The calamari variables dictionary is returned as an autofac service that is generated
    /// using the same arguments that would be passed to a command. This means we have two places
    /// that build option sets: the command that would use the variables object, and the module that
    /// creates the object.
    ///
    /// To remove copy/paste between these classes, this interface allows the options to be set
    /// from one location.
    /// </summary>
    public interface IVariableDictionaryUtils
    {
        /// <summary>
        /// Populate an option set with the arguments required to build a calamari variable dictionary.
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
