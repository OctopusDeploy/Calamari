#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Aws;
using Calamari.Testing;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    /// <remarks>
    /// This is a special test fixture, that gets remotely executed on the cluster created by the test 
    /// Calamari.Tests.KubernetesFixtures.KubernetesContextScriptWrapperLiveFixture.UsingEc2Instance
    /// (see Terraform/EC2/ec2.kubernetes.tf and Terraform/EC2/test.sh)
    ///
    /// It's allowed to access environment variables directly because of this specialness.
    /// It's ignored from direct runs locally or on CI using the [Explicit] attribute.
    /// </remarks>
    [TestFixture]
    [Explicit]
    public class KubernetesContextScriptWrapperLiveFixtureForAmazon : KubernetesContextScriptWrapperLiveFixtureBase
    {
        string eksIamRolArn;
        string region;
        string eksClusterArn;
        string eksClusterName;
        string eksClusterEndpoint;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            region = Environment.GetEnvironmentVariable("AWS_REGION");
            eksIamRolArn = Environment.GetEnvironmentVariable("AWS_IAM_ROLE_ARN");
            eksClusterArn = Environment.GetEnvironmentVariable("AWS_CLUSTER_ARN");
            eksClusterName = Environment.GetEnvironmentVariable("AWS_CLUSTER_NAME");
            eksClusterEndpoint = Environment.GetEnvironmentVariable("AWS_CLUSTER_URL");
        }

        [Test]
        public void AuthoriseWithAmazonEC2Role()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, eksClusterEndpoint);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);
            variables.Set(SpecialVariables.SkipTlsVerification, Boolean.TrueString);
            variables.Set("Octopus.Action.Aws.AssumeRole", Boolean.FalseString);
            variables.Set("Octopus.Action.Aws.Region", region);

            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void DiscoverKubernetesClusterWithEc2InstanceCredentialsAndIamRole()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials { Type = "worker" },
                Role = new AwsAssumedRole
                {
                    Type = "assumeRole",
                    Arn = eksIamRolArn
                },
                Regions = new[] { region }
            };

            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", eksClusterArn },
                { "clusterName", eksClusterName },
                { "clusterUrl", eksClusterEndpoint },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsAssumeRole", bool.TrueString },
                { "awsAssumeRoleArn", eksIamRolArn },
            };

            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                serviceMessageProperties);
        }

        [Test]
        public void DiscoverKubernetesClusterWithEc2InstanceCredentialsAndNoIamRole()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials { Type = "worker" },
                Role = new AwsAssumedRole { Type = "noAssumedRole" },
                Regions = new []{region}
            };
                
            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", eksClusterArn },
                { "clusterName", eksClusterName },
                { "clusterUrl", eksClusterEndpoint },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsAssumeRole", bool.FalseString }
            };

            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                serviceMessageProperties);
        }
    }
}
#endif