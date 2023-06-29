using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Octostache.Templates;

namespace Calamari.Deployment
{
    public class ConventionProcessor
    {
        readonly RunningDeployment deployment;
        readonly List<IConvention> conventions;
        readonly ILog log;

        public ConventionProcessor(RunningDeployment deployment, List<IConvention> conventions, ILog log)
        {
            this.deployment = deployment;
            this.conventions = conventions;
            this.log = log;
        }

        public void RunConventions(bool logExceptions = true)
        {
            var installConventions = conventions.OfType<IInstallConvention>();
            var rollbackConventions = conventions.OfType<IRollbackConvention>().ToList();
            try
            {
                // Now run the "conventions", for example: Deploy.ps1 scripts, XML configuration, and so on
                RunInstallConventions(installConventions);

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup(rollbackConventions);
            }
            catch (Exception installException)
            {
                if (logExceptions)
                {
                    if (installException is CommandException || installException is RecursiveDefinitionException)
                        log.Verbose(installException.ToString());
                    else
                        Console.Error.WriteLine(installException);
                }

                deployment.Error(installException);

                if (rollbackConventions.Any())
                {
                    Console.Error.WriteLine("Running rollback conventions...");

                    try
                    {
                        // Rollback conventions include tasks like DeployFailed.ps1
                        RunRollbackConventions(rollbackConventions);

                        // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                        RunRollbackCleanup(rollbackConventions);
                    }
                    catch (Exception rollbackException)
                    {
                        //if the "rollback" exception message is identical to the exception we got during "install", dont log it
                        if (rollbackException.Message != installException.Message)
                        {
                            if (rollbackException is CommandException || rollbackException is RecursiveDefinitionException)
                                log.Verbose(installException.ToString());
                            else
                                Console.Error.WriteLine(rollbackException);
                        }
                    }
                }

                throw;
            }
        }


        void RunInstallConventions(IEnumerable<IInstallConvention> installConventions)
        {
            foreach (var convention in installConventions)
            {
                convention.Install(deployment);

                if (deployment.Variables.GetFlag(Common.Plumbing.Variables.KnownVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
            }
        }

        void RunRollbackConventions(IEnumerable<IRollbackConvention> rollbackConventions)
        {
            foreach (var convention in rollbackConventions)
            {
                convention.Rollback(deployment);
            }
        }

        void RunRollbackCleanup(IEnumerable<IRollbackConvention> rollbackConventions)
        {
            foreach (var convention in rollbackConventions)
            {
                if (deployment.Variables.GetFlag(Common.Plumbing.Variables.KnownVariables.Action.SkipRemainingConventions))
                {
                    break;
                }

                convention.Cleanup(deployment);
            }
        }
    }
}