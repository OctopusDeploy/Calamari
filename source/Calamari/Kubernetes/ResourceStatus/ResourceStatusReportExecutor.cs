#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportExecutor
    {
        private readonly IVariables variables;
        private readonly ILog log;
        private readonly RunningResourceStatusCheck.Factory runningResourceStatusCheckFactory;

        public ResourceStatusReportExecutor(
            IVariables variables,
            ILog log,
            RunningResourceStatusCheck.Factory runningResourceStatusCheckFactory)
        {
            this.variables = variables;
            this.log = log;
            this.runningResourceStatusCheckFactory = runningResourceStatusCheckFactory;
        }

        public IRunningResourceStatusCheck Start(IEnumerable<ResourceIdentifier> resources)
        {
            return DoResourceCheck(resources);
        }

        public IRunningResourceStatusCheck Start()
        {
            log.Info("Resource Status Check Started: Waiting for resources to be applied.");
            return DoResourceCheck();
        }

        private IRunningResourceStatusCheck DoResourceCheck(IEnumerable<ResourceIdentifier> initialResources = null)
        {
            initialResources = initialResources ?? Enumerable.Empty<ResourceIdentifier>();
            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var timeout = timeoutSeconds == 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromSeconds(timeoutSeconds);

            return runningResourceStatusCheckFactory(timeout, new Options {  WaitForJobs = waitForJobs}, initialResources);
        }
    }
}
#endif