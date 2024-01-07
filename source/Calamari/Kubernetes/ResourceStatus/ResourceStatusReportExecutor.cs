using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IResourceStatusReportExecutor
    {
        IRunningResourceStatusCheck Start(int timeoutSeconds, bool waitForJobs, IEnumerable<ResourceIdentifier> initialResources = null);
    }
    
    public class ResourceStatusReportExecutor : IResourceStatusReportExecutor
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

        public IRunningResourceStatusCheck Start(int timeoutSeconds, bool waitForJobs, IEnumerable<ResourceIdentifier> initialResources = null)
        {
            initialResources = initialResources ?? Enumerable.Empty<ResourceIdentifier>();
            
            var timeout = timeoutSeconds == 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromSeconds(timeoutSeconds);

            return runningResourceStatusCheckFactory(timeout, new Options {  WaitForJobs = waitForJobs}, initialResources);
        }
    }
}