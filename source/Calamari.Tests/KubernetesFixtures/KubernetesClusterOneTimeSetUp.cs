#if NETCORE
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Tests.KubernetesFixtures.Tools;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [SetUpFixture]
    public class KubernetesClusterOneTimeSetUp
    {
        KubernetesClusterInstaller installer;
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var toolDownloader = new RequiredToolDownloader(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, KubernetesTestsGlobalContext.Instance.Logger);
            var (kindExePath, helmExePath, kubeCtlPath) = await toolDownloader.DownloadRequiredTools(CancellationToken.None);

            installer = new KubernetesClusterInstaller(KubernetesTestsGlobalContext.Instance.TemporaryDirectory, kindExePath, helmExePath, kubeCtlPath, KubernetesTestsGlobalContext.Instance.Logger);
            await installer.Install();

            KubernetesTestsGlobalContext.Instance.SetToolExePaths(helmExePath, kubeCtlPath);
            KubernetesTestsGlobalContext.Instance.KubeConfigPath = installer.KubeConfigPath;

            var details = installer.ExtractLoginDetails();
            KubernetesTestsGlobalContext.Instance.ClusterUser = details.ClusterUser;
            KubernetesTestsGlobalContext.Instance.ClusterEndpoint = details.ClusterEndpoint;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            installer.Dispose();
            KubernetesTestsGlobalContext.Instance.Dispose();
        }
    }
}
#endif