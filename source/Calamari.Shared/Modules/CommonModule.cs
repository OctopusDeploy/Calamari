using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using System.IO;
using Calamari.Integration.Certificates;
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

        private bool AnyValuesSet()
        {
            return !string.IsNullOrWhiteSpace(variablesFile) ||
                !string.IsNullOrWhiteSpace(base64Variables) ||
                !string.IsNullOrWhiteSpace(sensitiveVariablesFile) ||
                !string.IsNullOrWhiteSpace(sensitiveVariablesPassword);
        }

        protected override void Load(ContainerBuilder builder)
        {
            // If any values have been supplied, attempt to build the CalamariVariableDictionary
            if (AnyValuesSet())
            {
                builder.RegisterInstance(new CalamariVariableDictionary(
                    variablesFile,
                    sensitiveVariablesFile,
                    sensitiveVariablesPassword,
                    base64Variables))
                    .AsSelf()
                    .As<VariableDictionary>();
                
            }
            // Otherwise return an empty CalamariVariableDictionary
            else
            {
                builder.RegisterInstance(new CalamariVariableDictionary())
                    .AsSelf()
                    .As<VariableDictionary>();
            }

            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().InstancePerLifetimeScope();
            builder.RegisterType<LogWrapper>().As<ILog>().InstancePerLifetimeScope();
        }
    }
}
