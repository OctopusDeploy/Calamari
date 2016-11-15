using System.Linq;
using System.Text;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.Conventions.RunScript
{
    public class RunScriptFeature : IFeature
    {       
        public void ConfigureInstallSequence(IVariableDictionary variables, IConventionSequence<IInstallConvention> sequence)
        {
            sequence
                .Run(Hello)
                .RunConditional(HasPackage, CommonConventions.PackageExtraction)
                .Run(CommonConventions.VariableReplacement, variables.Get(SpecialVariables.Action.Script.Path))
                .Run(CommonConventions.ExecuteScript, variables.Get(SpecialVariables.Action.Script.Path), variables.Get(SpecialVariables.Action.Script.Parameters));
        }

        public void Rolback(IVariableDictionary variables)
        {
        }

        void Hello(IVariableDictionary variables)
        {
            
        }


        private static bool HasPackage(IVariableDictionary t)
        {
            return !string.IsNullOrEmpty(t.Get(SpecialVariables.Package.NuGetPackageId));
        }
    }
}
