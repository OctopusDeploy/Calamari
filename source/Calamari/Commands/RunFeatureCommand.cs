using System.IO;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Features;
using Calamari.Features.Conventions;
using Calamari.Integration.Processes;
using Calamari.Util;
using Calamari.Deployment;
using Calamari.Extensibility.RunScript;
using System.Reflection;

namespace Calamari.Commands
{
    [Command("run-feature", Description = "Extracts and installs a deployment package")]
    public class RunFeatureCommand : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string featureName;

        public RunFeatureCommand()
        {
            Options.Add("feature=",
                "The name of the feature that will be loaded from available assembelies and invoked.",
                v => featureName = v);
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
        }


        internal int Execute(string featureName, CalamariVariableDictionary variables)
        {
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();


            var al = new AssemblyLoader();

            al.RegisterAssembly(typeof(RunScriptFeature).GetTypeInfo().Assembly);
            al.RegisterAssembly(typeof(PackageDeploymentFeatureRunner.DeployPackageFeature).GetTypeInfo().Assembly);

            var container = CreateContainer(variables);
            var dpb = new DepencencyInjectionBuilder(container);


            var type = new FeatureLocator(al).Locate(featureName);

            var feature = dpb.BuildConvention(type);

            try
            {
                var deploymentFeature = feature as IPackageDeploymentFeature;

                if (deploymentFeature != null)
                {
                    var runner = new PackageDeploymentFeatureRunner(deploymentFeature);
                    runner.Install(variables);
                }
                else
                {
                    (feature as IFeature)?.Install(variables);
                }
            }
            catch
            {
                (feature as IRollBackFeature)?.Rollback(variables);
                throw;
            }
            return 0;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            Guard.NotNullOrWhiteSpace(featureName, "No feature was specified. Please pass a value for the `--feature` option.");

            return Execute(featureName, new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword));
        }

        private static CalamariContainer CreateContainer(CalamariVariableDictionary variables)
        {
            
            var container = new CalamariContainer();
            container.RegisterInstance<IVariableDictionary>(variables);
            container.RegisterInstance<CalamariVariableDictionary>(variables);

            (new MyModule()).Register(container);
            return container;
        }
    }
}