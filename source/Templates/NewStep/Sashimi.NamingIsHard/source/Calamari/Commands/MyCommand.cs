using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.NamingIsHard.Behaviours;

namespace Calamari.NamingIsHard.Commands
{
    [Command("my-command-name", Description = "My super command")]
    public class MyCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<MyFirstBehaviour>();
        }
    }
}