#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public class ResourceStatusReportExecutor
    {
        private readonly IVariables variables;
        private readonly RunningResourceStatusCheck.Factory runningResourceStatusCheckFactory;

        public ResourceStatusReportExecutor(
            IVariables variables,
            RunningResourceStatusCheck.Factory runningResourceStatusCheckFactory)
        {
            this.variables = variables;
            this.runningResourceStatusCheckFactory = runningResourceStatusCheckFactory;
        }

        public IRunningResourceStatusCheck Start(IEnumerable<ResourceIdentifier> initialResources = null)
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