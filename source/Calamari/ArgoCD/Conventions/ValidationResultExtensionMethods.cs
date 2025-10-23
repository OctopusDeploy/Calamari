using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions
{
    static class ValidationResultExtensionMethods
    {
        public static void Action(this ValidationResult validationResult, ILog log)
        {
            foreach (var warning in validationResult.Warnings)
            {
                log.Warn(warning);
            }

            //We only need to throw one
            foreach (var error in validationResult.Errors)
            {
                throw new CommandException(error);
            }
        }
    }
}