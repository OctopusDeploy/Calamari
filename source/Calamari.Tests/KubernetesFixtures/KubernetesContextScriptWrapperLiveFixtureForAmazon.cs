#if NETCORE
using System;
using Calamari.Kubernetes;
using Calamari.Testing;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Explicit]
    public class KubernetesContextScriptWrapperLiveFixtureForAmazon : KubernetesContextScriptWrapperLiveFixtureBase
    {
        [Test]
        public void AuthoriseWithAmazonEC2Role()
        {
            // This fixture is marked Explicit, so it's always ignored unless you explicitly run it.
            // Please BYO EKS Cluster and fill in the variables below if you want to run it.
            var eksClusterUrl = "";
            var eksClusterName = "";
            var eksClusterRegion = "";

            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, eksClusterUrl);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);
            variables.Set(SpecialVariables.SkipTlsVerification, Boolean.TrueString);
            variables.Set("Octopus.Action.Aws.AssumeRole", Boolean.FalseString);
            variables.Set("Octopus.Action.Aws.Region", eksClusterRegion);

            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }
    }
}
#endif