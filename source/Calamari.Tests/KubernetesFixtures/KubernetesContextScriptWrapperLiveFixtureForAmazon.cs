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
            // This is a special test fixture, that gets remotely executed on the cluster created by the test 
            // Calamari.Tests.KubernetesFixtures.KubernetesContextScriptWrapperLiveFixture.UsingEc2Instance
            // (see Terraform/EC2/ec2.kubernetes.tf and Terraform/EC2/test.sh)
            //
            // It's allowed to access environment variables directly because of this specialness.
            // It's ignored from direct runs locally or on CI using the [Explicit] attribute.
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, Environment.GetEnvironmentVariable("AWS_CLUSTER_URL"));
            variables.Set(SpecialVariables.EksClusterName, Environment.GetEnvironmentVariable("AWS_CLUSTER_NAME"));
            variables.Set(SpecialVariables.SkipTlsVerification, Boolean.TrueString);
            variables.Set("Octopus.Action.Aws.AssumeRole", Boolean.FalseString);
            variables.Set("Octopus.Action.Aws.Region", Environment.GetEnvironmentVariable("AWS_REGION"));

            var wrapper = CreateWrapper();
            TestScriptAndVerifyCluster(wrapper, "Test-Script");
        }
    }
}
#endif