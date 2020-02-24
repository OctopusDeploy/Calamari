using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Core;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Packages;

namespace Calamari.Commands
{
    public interface IConventionFactory
    {
        IInstallConvention ConfiguredScript(string deploymentStage);
        IInstallConvention ExtractPackageToStagingDirectory(bool extractToSubdirectory = true);
        IInstallConvention SubstituteInFilesBasedOnVariableValues();
        IInstallConvention SubstituteInFiles(Func<RunningDeployment, bool> predicate, Func<RunningDeployment, IEnumerable<string>> fileTargetFactory);
    }

    public class ConventionFactory : IConventionFactory
    {
        readonly ILifetimeScope lifetimeScope;

        public ConventionFactory(ILifetimeScope lifetimeScope)
        {
            this.lifetimeScope = lifetimeScope;
        }

        public IInstallConvention ConfiguredScript(string deploymentStage)
            => lifetimeScope.Resolve<ConfiguredScriptConvention>(
                new NamedParameter("deploymentStage", deploymentStage)
            );
        
        public IInstallConvention ExtractPackageToStagingDirectory(bool extractToSubdirectory = true)
            => lifetimeScope.Resolve<ExtractPackageToStagingDirectoryConvention>(
                new NamedParameter("extractToSubdirectory", extractToSubdirectory)
            );

        public IInstallConvention SubstituteInFilesBasedOnVariableValues()
            => lifetimeScope.Resolve<SubstituteInFilesConvention>();

        public IInstallConvention SubstituteInFiles(Func<RunningDeployment, bool> predicate, Func<RunningDeployment, IEnumerable<string>> fileTargetFactory)
            => lifetimeScope.Resolve<SubstituteInFilesConvention>(
                TypedParameter.From(predicate),
                TypedParameter.From(fileTargetFactory)
            );
    }
}