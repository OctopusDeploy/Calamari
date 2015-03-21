using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.Conventions;

namespace Calamari.Deployment
{
    public class ConventionProcessor
    {
        readonly RunningDeployment deployment;
        readonly List<IConvention> conventions;

        public ConventionProcessor(RunningDeployment deployment, List<IConvention> conventions)
        {
            this.deployment = deployment;
            this.conventions = conventions;
        }

        public void RunConventions()
        {
            try
            {
                // Now run the "conventions", for example: Deploy.ps1 scripts, XML configuration, and so on
                RunInstallConventions();

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Console.Error.WriteLine("Running rollback conventions...");

                deployment.Error(ex);

                // Rollback conventions include tasks like DeployFailed.ps1
                RunRollbackConventions();

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup();

                throw;
            }
        }

        void RunInstallConventions()
        {
            foreach (var convention in conventions.OfType<IInstallConvention>())
            {
                convention.Install(deployment);
            }
        }

        void RunRollbackConventions()
        {
            foreach (var convention in conventions.OfType<IRollbackConvention>())
            {
                convention.Rollback(deployment);
            }
        }

        void RunRollbackCleanup()
        {
            foreach (var convention in conventions.OfType<IRollbackConvention>())
            {
                convention.Cleanup(deployment);
            }
        }
    }
}