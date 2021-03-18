using System;
using System.Collections.Generic;
using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Scripting
{
    [Command("run-script", Description = "Invokes a script")]
    public class RunScriptCommand : PipelineCommand
    {
        protected override bool IncludeConfiguredScriptBehaviour => false;
        protected override bool IncludePackagedScriptBehaviour => false;

        protected override IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            yield return resolver.Create<WriteVariablesToFileBehaviour>();
        }

        protected override IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            yield return resolver.Create<StageScriptPackagesBehaviour>();
        }

        protected override IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            yield return resolver.Create<SubstituteScriptSourceBehaviour>();
        }

        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<ExecuteScriptBehaviour>();
        }

        protected override IEnumerable<IOnFinishBehaviour> OnFinish(OnFinishResolver resolver)
        {
            yield return resolver.Create<AddJournalEntryBehaviour>();
            yield return resolver.Create<ThrowScriptErrorIfNeededBehaviour>();
        }
    }
}