using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using System.IO;
using Octostache;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register common objects
    /// </summary>
    public class CommonModule : Module
    {
        private static readonly IVariableDictionaryUtils VariableDictionaryUtils = new VariableDictionaryUtils();
        private readonly OptionSet optionSet = new OptionSet();
        private string variablesFile;
        private string base64Variables;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public CommonModule(string[] args)
        {
            VariableDictionaryUtils.PopulateOptions(
                optionSet,
                v => variablesFile = Path.GetFullPath(v),
                v => base64Variables = v,
                v => sensitiveVariablesFile = v,
                v => sensitiveVariablesPassword = v);
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
                    base64Variables)).AsSelf().As<VariableDictionary>();
            }
        }
    }
}
