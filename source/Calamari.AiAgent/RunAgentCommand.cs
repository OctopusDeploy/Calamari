using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AiAgent
{
    [Command("run-agent", Description = "Invokes an AI agent")]
    public class RunAgentCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield break;
        }
    }
}
