using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IContributeToPackageDeployment
    {
        PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context, ITaskLog taskLog);
    }

    public abstract class PackageContributionResult
    {
        public static PackageContributionResult SkipPackageDeployment()
        {
            return new SkipPackageDeploymentContributionResult();
        }

        public static PackageContributionResult RedirectToHandler<THandle>() where THandle : IActionHandler
        {
            return new RedirectToHandlerContributionResult(typeof(THandle));
        }

        public static PackageContributionResult DoDefaultPackageDeployment()
        {
            return new DoDefaultPackageDeploymentContributionResult();
        }
    }

    public class SkipPackageDeploymentContributionResult : PackageContributionResult
    {
        internal SkipPackageDeploymentContributionResult()
        {
        }
    }

    public class DoDefaultPackageDeploymentContributionResult : PackageContributionResult
    {
        internal DoDefaultPackageDeploymentContributionResult()
        {
        }
    }

    public class RedirectToHandlerContributionResult : PackageContributionResult
    {
        internal RedirectToHandlerContributionResult(Type handler)
        {
            Handler = handler;
        }

        public Type Handler { get; }
    }
}