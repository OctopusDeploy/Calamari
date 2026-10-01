namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Options to configure Kubernetes resource status checks behaviour
    /// </summary>
    public class Options
    {
        /// <summary>
        /// When set to true, wait for Jobs to complete and report in-progress, success or failure states accordingly.
        /// Otherwise Jobs are always treated as being successful once created.
        /// </summary>
        public bool WaitForJobs { get; set; }

        public bool PrintVerboseKubectlOutputOnError { get; set; }

        public bool PrintVerboseOutput { get; set; }

        /// <summary>
        /// Break-glass toggle that reverts resource status checks to the legacy logic that predates
        /// alignment with the gitops-engine health checks. Disabled by default.
        /// </summary>
        public bool EnableLegacyResourceStatusChecks { get; set; }
    }
}