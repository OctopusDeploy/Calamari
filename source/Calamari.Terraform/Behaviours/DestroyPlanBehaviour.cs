using System;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Terraform.Behaviours
{
    class DestroyPlanBehaviour : PlanBehaviour
    {
        public DestroyPlanBehaviour(ILog log,
                                    ICalamariFileSystem fileSystem,
                                    ICommandLineRunner commandLineRunner) : base(log, fileSystem, commandLineRunner)
        {
        }

        protected override string ExtraParameter => "-destroy";
    }
}