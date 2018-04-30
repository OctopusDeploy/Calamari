using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using System.IO;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register common objects
    /// </summary>
    public class CommonModule : Module
    {
        private readonly OptionSet optionSet = new OptionSet();
        private string variablesFile;
        private string base64Variables;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public CommonModule(string[] args)
        {
            optionSet.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            optionSet.Add("base64Variables=", "JSON string containing variables.", v => base64Variables = v);
            optionSet.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            optionSet.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            optionSet.Parse(args);
        }

        protected override void Load(ContainerBuilder builder)
        {
            // If the variables file was not defined, return empty variables.
            // This is great for testing, because you don't need to worry
            // about a valid variables file
            if (string.IsNullOrWhiteSpace(variablesFile))
            {
                builder.RegisterInstance(new CalamariVariableDictionary()).AsSelf();
            }
            // Otherwise return the populated variables
            else
            {
                builder.RegisterInstance(new CalamariVariableDictionary(
                    variablesFile,
                    sensitiveVariablesFile,
                    sensitiveVariablesPassword,
                    base64Variables)).AsSelf();
            }
        }
    }
}
