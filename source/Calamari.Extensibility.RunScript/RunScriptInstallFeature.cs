using Calamari.Extensibility.Features;

namespace Calamari.Extensibility.RunScript
{
    [Feature("RunScript", "I Am A Run Script")]
    public class RunScriptInstallFeature : IFeature
    {
        private readonly IPackageExtractor extractor;
        private readonly IScriptExecution executor;
        private readonly IFileSubstitution substitutor;

        public RunScriptInstallFeature(IPackageExtractor extractor, IScriptExecution executor, IFileSubstitution substitutor)
        {
            this.extractor = extractor;
            this.executor = executor;
            this.substitutor = substitutor;
        }

        public void Install(IVariableDictionary variables)
        {
            var script = variables.Get(SpecialVariables.Action.Script.Path);
            var parameters = variables.Get(SpecialVariables.Action.Script.Parameters);
            var package = variables.Get(SpecialVariables.Action.Script.PackagePath);

            if (!string.IsNullOrWhiteSpace(package))
            {
                extractor.Extract(package, PackageExtractionDestination.WorkingDirectory);
            }

            substitutor.PerformSubstitution(script);
            executor.Invoke(script, parameters);
        }
    }
}
