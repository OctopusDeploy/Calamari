using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Octostache.Templates;

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
            catch (Exception installException)
            {
                if (installException is CommandException || installException is RecursiveDefinitionException)
                    Console.Error.WriteLine(installException.Message);
                else
                    Console.Error.WriteLine(installException);

                Console.Error.WriteLine("Running rollback conventions...");

                deployment.Error(installException);

                try
                {
                    // Rollback conventions include tasks like DeployFailed.ps1
                    RunRollbackConventions();

                    // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                    RunRollbackCleanup();
                }
                catch (Exception rollbackException)
                {
                    if (rollbackException is CommandException)
                        Console.Error.WriteLine(rollbackException.Message);
                    else if (rollbackException is RecursiveDefinitionException && rollbackException.Message != installException.Message)
                        //dont duplicate these error messages
                        Console.Error.WriteLine(rollbackException.Message);
                    else if (!(rollbackException is RecursiveDefinitionException))
                        Console.Error.WriteLine(rollbackException);
                }
                throw;
            }
        }
        

        void RunInstallConventions()
        {
            foreach (var convention in conventions.OfType<IInstallConvention>())
            {
                convention.Install(deployment);

                if (deployment.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
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
                if (deployment.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions))
                {
                    break;
                }

                convention.Cleanup(deployment);
            }
        }
    }
}