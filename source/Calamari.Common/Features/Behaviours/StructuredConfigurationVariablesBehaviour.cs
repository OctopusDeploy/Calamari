using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Behaviours
{
    public class StructuredConfigurationVariablesBehaviour : IBehaviour
    {
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        private readonly string subdirectory;

        public StructuredConfigurationVariablesBehaviour(IStructuredConfigVariablesService structuredConfigVariablesService, string subdirectory = "")
        {
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.subdirectory = subdirectory;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.StructuredConfigurationVariables);
        }

        public Task Execute(RunningDeployment context)
        {
            structuredConfigVariablesService.ReplaceVariables(Path.Combine(context.CurrentDirectory, subdirectory));

            return Task.CompletedTask;
        }
    }
}