#if NETCORE
using System;
using Calamari.Kubernetes;
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
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, Environment.GetEnvironmentVariable("AWS_CLUSTER_URL"));
            variables.Set(SpecialVariables.EksClusterName, Environment.GetEnvironmentVariable("AWS_CLUSTER_NAME"));
            variables.Set(SpecialVariables.SkipTlsVerification, Boolean.TrueString);
            variables.Set("Octopus.Action.Aws.AssumeRole", Boolean.FalseString);
            variables.Set("Octopus.Action.Aws.Region", "ap-southeast-2");

            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }
    }
}
#endif