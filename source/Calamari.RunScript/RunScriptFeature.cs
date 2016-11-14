using System.Linq;
using System.Text;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.RunScript
{
    public class RunScriptFeature : IFeature
    {       
        public void ConfigureInstallSequence(IVariableDictionary variables, IConventionSequence<IInstallConvention> sequence)
        {
            sequence
                .Run(Hello)
                .RunConditional(HasPackage, CommonConventions.PackageExtraction)
                .Run(CommonConventions.VariableReplacement, variables.Get("ScriptName"))
                .Run(CommonConventions.ExecuteScript, variables.Get("ScriptName"), variables.Get("ScriptParameters"));
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
