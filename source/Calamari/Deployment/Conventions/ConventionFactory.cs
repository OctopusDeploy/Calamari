using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Commands;

namespace Calamari.Deployment.Conventions
{
    public class ConventionFactory : IConventionFactory
    {
        readonly IVariables variables;
        readonly SubstituteInFilesConvention.Factory substituteInFilesConventionFactory;

        public ConventionFactory(
            IVariables variables, 
            SubstituteInFilesConvention.Factory substituteInFilesConventionFactory
            )
        {
            this.variables = variables;
            this.substituteInFilesConventionFactory = substituteInFilesConventionFactory;
        }

        public IInstallConvention SubstituteInFilesBasedOnVariableValues()
        {
            var enabled = variables.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled);
            if (enabled)
                return SubstituteInFiles((deployment) => deployment.Variables.GetPaths(SpecialVariables.Package.SubstituteInFilesTargets));

            return new SkippedConvention<SubstituteInFilesConvention>();
        }

        public IInstallConvention SubstituteInFiles(Func<RunningDeployment, IEnumerable<string>> fileTargetFactory)
            => substituteInFilesConventionFactory(fileTargetFactory);
    }
}