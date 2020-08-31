using System;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    public class StructuredConfigurationVariablesBehaviour : IBehaviour
    {
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;

        public StructuredConfigurationVariablesBehaviour(IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            this.structuredConfigVariablesService = structuredConfigVariablesService;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            var features = context.Variables.GetStrings(KnownVariables.Package.EnabledFeatures)
                                  .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            return features.Contains(KnownVariables.Features.StructuredConfigurationVariables);
        }

        public Task Execute(RunningDeployment context)
        {
            structuredConfigVariablesService.ReplaceVariables(context);

            return this.CompletedTask();
        }
    }
}