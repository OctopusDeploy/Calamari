using System;
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
            return context.Variables.GetFlag(KnownVariables.Package.JsonConfigurationVariablesEnabled);
        }

        public Task Execute(RunningDeployment context)
        {
            ExecuteSync(context);

            return this.CompletedTask();
        }

        public void ExecuteSync(RunningDeployment context)
        {
            structuredConfigVariablesService.ReplaceVariables(context);
        }
    }
}