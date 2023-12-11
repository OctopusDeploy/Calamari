using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Aws.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Authentication
{
    public class SetupKubectlAuthenticationAwsFixture
    {
        private readonly string workingDirectory = Path.Combine("working", "directory");
        private const string Namespace = "my-cool-namespace";

        private const string CurrentAwsVersion = "aws-cli/2.14.2";
        private const string OlderAwsVersion = "aws-cli/1.16.155";
        private const string InvalidAwsVersion = "aws-cli/not-a-version";

        private const string EksClusterName = "my-cool-eks-cluster-name";
        private const string AwsRegion = "southwest";
        private const string AwsClusterUrl = "http://www." + AwsRegion + ".eks.amazonaws.com";
        private const string InvalidAwsClusterUrl = "http://www." + AwsRegion + "..eks.amazonaws.com";

        private IVariables variables;
        private ILog log;
        private ICommandLineRunner commandLineRunner;
        private IKubectl kubectl;
        private ICalamariFileSystem fileSystem;
        private Dictionary<string, string> environmentVars;

        private SetupKubectlAuthenticationFixture.Invocations invocations;

        // TODO: Strip down/commonize
        [SetUp]
        public void Setup()
        {
            invocations = new SetupKubectlAuthenticationFixture.Invocations();
            invocations.AddLogMessageFor("which", "kubelogin", "kubelogin");
            invocations.AddLogMessageFor("where", "kubelogin", "kubelogin");
            AddLogForAwsVersion(CurrentAwsVersion);

            variables = new CalamariVariables();

            log = Substitute.For<ILog>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>())
                .Returns(
                    x =>
                    {
                        var invocation = x.Arg<CommandLineInvocation>();
                        var isSuccess = true;
                        string logMessage = null;
                        if (invocation.Executable != "chmod")
                        {
                            isSuccess = invocations.TryAdd(invocation.Executable, invocation.Arguments, out logMessage);
                        }
                        if (logMessage != null)
                            invocation.AdditionalInvocationOutputSink?.WriteInfo(logMessage);
                        return new CommandResult(
                            invocation.Executable,
                            isSuccess ? 0 : 1,
                            workingDirectory: workingDirectory);
                    });
            kubectl = Substitute.For<IKubectl>();
            kubectl.ExecutableLocation.Returns("kubectl");
            kubectl.TrySetKubectl().Returns(true);
            kubectl.When(x => x.ExecuteCommandAndAssertSuccess(Arg.Any<string[]>()))
                .Do(
                    x =>
                    {
                        var args = x.Arg<string[]>();
                        if (args != null)
                            invocations.TryAdd("kubectl", string.Join(" ", args), out var _);
                    });
            fileSystem = Substitute.For<ICalamariFileSystem>();

            variables.Set(SpecialVariables.ClusterUrl, AwsClusterUrl);
            variables.Set(SpecialVariables.EksClusterName, EksClusterName);
            variables.Set(SpecialVariables.Namespace, Namespace);

            environmentVars = new Dictionary<string, string>
            {
                { "AWS_ACCESS_KEY_ID", "access_key" },
                { "AWS_SECRET_ACCESS_KEY", "secret_key" },
                { "AWS_REGION", "region" }
            };
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
                    SetKubectlCredentialsWithAwsCliInvocation,
                    GetNamespaceInvocation
                });

            var result = CreateSut().Execute(AccountTypes.AmazonWebServicesAccount);

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

            var expectedInvocations = SetupClusterContextInvocations(AwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                new List<(string, string)>
                {
                    GetAwsTokenInvocation,
                    SetKubectlCredentialsWithAwsCliInvocation,
                    GetNamespaceInvocation
                });

            var result = CreateSut().Execute(null);

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

            var result = CreateSut().Execute(AccountTypes.AmazonWebServicesAccount);

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

            var result = CreateSut().Execute(null);

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

            var result = CreateSut().Execute(AccountTypes.AmazonWebServicesAccount);

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

            var result = CreateSut().Execute(AccountTypes.AmazonWebServicesAccount);

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received()
                .Verbose(
                    Arg.Is<string>(
                        s => s.StartsWith(
                            $"Unable to authenticate to {AwsClusterUrl} using the aws cli. Failed with error message: 'not-a-version' is not a valid version string")));
        }

        [Test]
        public void FallsBackToIamAuthenticatorWithoutRegion()
        {
            variables.Set(SpecialVariables.ClusterUrl, InvalidAwsClusterUrl);

            AddLogForAwsEksGetToken();
            AddLogForWhichAws();

            var expectedInvocations = SetupClusterContextInvocations(InvalidAwsClusterUrl);
            expectedInvocations.AddRange(AwsCliInvocations());
            expectedInvocations.AddRange(
                new List<(string, string)>
                {
                    IamAuthenticatorInvocation,
                    GetNamespaceInvocation
                });

            var result = CreateSut().Execute(AccountTypes.AmazonWebServicesAccount);

            result.VerifySuccess();
            AssertInvocations(expectedInvocations);
            log.Received().Verbose("The EKS cluster Url specified should contain a valid aws region name");
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
                $"{{ \"apiVersion\": \"1.2.3\"}}");
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

        List<(string, string)> AwsCliInvocations()
        {
            return new List<(string, string)>
            {
                ("aws", "configure set aws_access_key_id access_key"),
                ("aws", "configure set aws_secret_access_key secret_key"),
                ("aws", "configure set aws_default_region region"),
                ("aws", "configure set aws_session_token"),
                ("aws", "--version")
            };
        }

        (string, string) IamAuthenticatorInvocation =>
            ("kubectl",
                $"config set-credentials octouser --exec-command=aws-iam-authenticator --exec-api-version=client.authentication.k8s.io/v1alpha1 --exec-arg=token --exec-arg=-i --exec-arg={EksClusterName}");

        (string, string ) GetAwsTokenInvocation =>
            ("aws", "eks get-token --cluster-name=my-cool-eks-cluster-name --region=southwest");

        (string, string) SetKubectlCredentialsWithAwsCliInvocation =>
            ("kubectl",
                "config set-credentials octouser --exec-command=aws --exec-arg=eks --exec-arg=get-token --exec-arg=--cluster-name=my-cool-eks-cluster-name --exec-arg=--region=southwest --exec-api-version=1.2.3");

        (string, string) GetNamespaceInvocation =>
            ("kubectl", $"get namespace {Namespace} --request-timeout=1m");

        private void AssertInvocations(List<(string, string)> expectedInvocations)
        {
            invocations.Where(x => x.Executable != "which" && x.Executable != "where")
                .Should()
                .BeEquivalentTo(expectedInvocations, opts => opts.WithStrictOrdering());
        }
    }
}