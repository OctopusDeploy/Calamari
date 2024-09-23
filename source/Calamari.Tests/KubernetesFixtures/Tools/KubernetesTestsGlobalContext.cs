#if NETCORE
using System;
using Calamari.Common.Plumbing.FileSystem;
using Serilog;

namespace Calamari.Tests.KubernetesFixtures.Tools
{
    public class KubernetesTestsGlobalContext : IDisposable
    {
        public static KubernetesTestsGlobalContext Instance { get; } = new KubernetesTestsGlobalContext();
        
        public TemporaryDirectory TemporaryDirectory { get; }
        
        public ILogger Logger { get; }
    
        public string KubeConfigPath { get; set; } = "<unset>";
    
        public string HelmExePath { get; private set; } = null!;
        public string KubeCtlExePath { get; private set; }= null!;
    
        public ClusterEndpoint ClusterEndpoint { get; set; }
        public ClusterUser ClusterUser { get; set; }
        KubernetesTestsGlobalContext()
        {
            TemporaryDirectory = TemporaryDirectory.Create();;

            Logger = Log.Logger;
        }
    
        public void Dispose()
        {
            TemporaryDirectory.Dispose();
        }
    
        public void SetToolExePaths(string helmExePath, string kubeCtlPath)
        {
            HelmExePath = helmExePath;
            KubeCtlExePath = kubeCtlPath;
        }
        
        
    }
}
#endif