﻿using System;
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
                    log.Verbose(installException.ToString());
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
                    //if the "rollback" exception message is identical to the exception we got during "install", dont log it
                    if (rollbackException.Message != installException.Message)
                    {
                        if (rollbackException is CommandException || rollbackException is RecursiveDefinitionException)
                            log.Verbose(installException.ToString());
                        else
                            Console.Error.WriteLine(rollbackException);
                    }
                }
                throw;
            }
        }


        void RunInstallConventions()
        {
            foreach (var convention in conventions.OfType<IInstallConvention>())
            {
                convention.Install(deployment);

                if (deployment.Variables.GetFlag(Common.Plumbing.Variables.KnownVariables.Action.SkipRemainingConventions))
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
                if (deployment.Variables.GetFlag(Common.Plumbing.Variables.KnownVariables.Action.SkipRemainingConventions))
                {
                    break;
                }

                convention.Cleanup(deployment);
            }
        }
    }
}