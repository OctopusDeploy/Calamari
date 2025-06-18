using System.Collections.Generic;
using System.Linq;
using Calamari.Aws.Deployment;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Authentication;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.KubernetesFixtures.Authentication
{
    public class SetupKubectlAuthenticationAwsFixture : BaseSetupKubectlAuthenticationFixture
    {
        private const string CurrentAwsVersion = "aws-cli/2.14.2";
        private const string OlderAwsVersion = "aws-cli/1.16.155";
        private const string InvalidAwsVersion = "aws-cli/not-a-version";

        private const string EksClusterName = "my-cool_eks-cluster-name";
        private const string AwsRegion = "southwest";
        private const string AwsClusterUrl = "https://" + EksClusterName + "." + AwsRegion + ".eks.amazonaws.com";
        private const string InvalidAwsClusterUrl = "https://www." + AwsRegion + "..eks.amazonaws.com";
        private const string HttpAwsClusterUrl = "http://" + EksClusterName + "." + AwsRegion + ".eks.amazonaws.com";
        private const string ClusterCname = "cluster.cname";

        [SetUp]
        public void Setup()
        {
            AddLogForAwsVersion(CurrentAwsVersion);

            variables.Set(SpecialVariables.ClusterUrl, AwsClusterUrl);
            variables.Set(SpecialVariables.EksClusterName, EksClusterName);
            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.AmazonWebServicesAccount);

            environmentVars.Add("AWS_ACCESS_KEY_ID", "access_key");
            environmentVars.Add("AWS_SECRET_ACCESS_KEY", "secret_key");
            environmentVars.Add("AWS_REGION", "region");
        }

        SetupKubectlAuthentication CreateSut() =>
            new SetupKubectlAuthentication(
                                           variables,
                                           log,
                                           commandLineRunner,
                                           kubectl,
                                           fileSystem,
                                           environmentVars,
                                           workingDirectory);

        [Test]
        public void AuthenticatesWithAwsCli()
        {
            AddLogForAwsEksGetToken();
            AddLogForWhichAws();
            AddLogForAwsVersion(CurrentAwsVersion);

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             GetAwsTokenInvocation,
                                             SetKubectlTokenInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
        }

        [Test]
        public void AuthenticatesWithAwsCli_UsesKubectlExecWhenFeatureToggleEnabled()
        {
            AddLogForAwsEksGetToken();
            AddLogForWhichAws();
            AddLogForAwsVersion(CurrentAwsVersion);

            kubectl.GetVersion().Returns(Maybe<SemanticVersion>.Some(new SemanticVersion(1, 29, 7)));

            variables.SetStrings(KnownVariables.EnabledFeatureToggles,
                                 new[]
                                 {
                                     FeatureToggle.KubernetesAuthAwsCliWithExecFeatureToggle.ToString()
                                 },
                                 ",");

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             SetKubectlCredentialsViaExecInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
        }

        [Test]
        public void AuthenticatesForInstanceRoleWithAwsCli()
        {
            AddLogForAwsEksGetToken();
            AddLogForWhichAws();
            AddLogForAwsVersion(CurrentAwsVersion);
            variables.AddFlag(AwsSpecialVariables.Authentication.UseInstanceRole, true);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "");

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             GetAwsTokenInvocation,
                                             SetKubectlTokenInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
        }

        [Test]
        public void FallsBackToIamAuthenticatorWithoutAwsCli()
        {
            AddLogForAwsEksGetToken();
            AddLogForAwsVersion(OlderAwsVersion);

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             IamAuthenticatorInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received().Verbose("Could not find the aws cli, falling back to the aws-iam-authenticator.");
        }

        [Test]
        public void AuthenticatesForInstanceRoleWithIamAuthenticator()
        {
            AddLogForAwsEksGetToken();
            AddLogForAwsVersion(OlderAwsVersion);
            variables.AddFlag(AwsSpecialVariables.Authentication.UseInstanceRole, true);

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             IamAuthenticatorInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received().Verbose("Could not find the aws cli, falling back to the aws-iam-authenticator.");
        }

        [Test]
        public void FallsBackToIamAuthenticatorWithOlderAwsCliVersion()
        {
            AddLogForAwsEksGetToken();
            AddLogForWhichAws();
            AddLogForAwsVersion(OlderAwsVersion);

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             IamAuthenticatorInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received()
               .Verbose(
                        "aws cli version: 1.16.155 does not support the \"aws eks get-token\" command. Please update to a version later than 1.16.156");
        }

        [Test]
        public void FailsWithInvalidAwsCliVersion()
        {
            AddLogForAwsEksGetToken();
            AddLogForWhichAws();
            AddLogForAwsVersion(InvalidAwsVersion);

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             IamAuthenticatorInvocation,
                                             GetNamespaceInvocation
                                         });

            CreateSut().Execute();

            AssertInvocations(expectedInvocations);
            log.Received()
               .Verbose(
                        Arg.Is<string>(
                                       s => s.StartsWith(
                                                         $"Unable to authenticate to {AwsClusterUrl} using the aws cli. Failed with error message: 'not-a-version' is not a valid version string")));
        }

        [Test]
        [TestCase(InvalidAwsClusterUrl)]
        [TestCase(HttpAwsClusterUrl)]
        [TestCase(ClusterCname)]
        public void FallsBackToIamAuthenticatorWithoutRegion(string clusterUrl)
        {
            variables.Set(SpecialVariables.ClusterUrl, clusterUrl);

            AddLogForAwsEksGetToken();
            AddLogForWhichAws();

            var expectedInvocations = SetupClusterContextInvocations(clusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                                         new List<(string, string)>
                                         {
                                             IamAuthenticatorInvocation,
                                             GetNamespaceInvocation
                                         });

            var result = CreateSut().Execute();

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received().Verbose("The EKS cluster Url specified should contain a valid aws region name");
            log.Received().Verbose("Attempting to authenticate with aws-iam-authenticator");
        }

        void AddLogForWhichAws()
        {
            invocations.AddLogMessageFor("which", "aws", "aws");
            invocations.AddLogMessageFor("where", "aws.exe", "aws");
        }

        void AddLogForAwsEksGetToken()
        {
            invocations.AddLogMessageFor(
                                         "aws",
                                         $"eks get-token --cluster-name={EksClusterName} --region={AwsRegion}",
                                         $"{{\"status\": {{\"token\": \"k8s-aws-v1.token\"}},\"apiVersion\":\"client.authentication.k8s.io/v1beta1\"}}");
        }

        void AddLogForAwsVersion(string version)
        {
            invocations.AddLogMessageFor("aws", "--version", version);
        }

        List<(string, string)> SetupClusterContextInvocations(string clusterUser)
        {
            return new List<(string, string)>
            {
                ("kubectl", $"config set-cluster octocluster --server={clusterUser}"),
                ("kubectl",
                 $"config set-context octocontext --user=octouser --cluster=octocluster --namespace={Namespace}"),
                ("kubectl", "config use-context octocontext"),
            };
        }

        List<(string, string)> AwsCliInvocations() => new List<(string, string)> { ("aws", "--version") };

        static (string, string) IamAuthenticatorInvocation =>
            ("kubectl",
             $"config set-credentials octouser --exec-command=aws-iam-authenticator --exec-api-version=client.authentication.k8s.io/v1alpha1 --exec-arg=token --exec-arg=-i --exec-arg={EksClusterName}");

        static (string, string ) GetAwsTokenInvocation
            => ("aws", $"eks get-token --cluster-name={EksClusterName} --region={AwsRegion}");

        static (string, string) SetKubectlTokenInvocation
            => ("kubectl", "config set-credentials octouser --token=k8s-aws-v1.token");

        static (string, string) SetKubectlCredentialsViaExecInvocation
            => ("kubectl", $"config set-credentials octouser --exec-command=aws --exec-arg=eks --exec-arg=get-token --exec-arg=--cluster-name={EksClusterName} --exec-arg=--region={AwsRegion} --exec-api-version=client.authentication.k8s.io/v1beta1");

        static (string, string) GetNamespaceInvocation => ("kubectl", $"get namespace {Namespace}");

        private void AssertInvocations(List<(string, string)> expectedInvocations)
        {
            invocations.Where(x => x.Executable != "which" && x.Executable != "where")
                       .Should()
                       .BeEquivalentTo(expectedInvocations, opts => opts.WithStrictOrdering());
        }
    }
}