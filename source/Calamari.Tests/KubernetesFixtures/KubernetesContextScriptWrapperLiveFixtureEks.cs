#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assent;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Deployment;
using Calamari.FeatureToggles;
using Calamari.Kubernetes.Commands;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.AWS;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using File = System.IO.File;
using KubernetesSpecialVariables = Calamari.Kubernetes.SpecialVariables;


namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class KubernetesContextScriptWrapperLiveFixtureEks: KubernetesContextScriptWrapperLiveFixture
    {
        private const string ResourcePackageFileName = "package.1.0.0.zip";
        private const string DeploymentFileName = "customresource.yml";
        private const string DeploymentFileName2 = "myapp-deployment.yml";
        private const string ServiceFileName = "myapp-service.yml";
        private const string ConfigMapFileName = "myapp-configmap1.yml";
        private const string ConfigMapFileName2 = "myapp-configmap2.yml";

        private const string SimpleDeploymentResourceType = "Deployment";
        private const string SimpleDeploymentResourceName = "nginx-deployment";
        private const string SimpleDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 80";

        private const string SimpleDeployment2ResourceName = "nginx-deployment";
        private const string SimpleDeploymentResource2 =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment2\nspec:\n  selector:\n    matchLabels:\n      app: nginx2\n  replicas: 1\n  template:\n    metadata:\n      labels:\n        app: nginx2\n    spec:\n      containers:\n      - name: nginx2\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 81\n";

        private const string InvalidDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\nbad text here\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 80\n";

        private const string FailToDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx-bad-container-name:1.14.2\n        ports:\n        - containerPort: 80\n";

        private const string SimpleServiceResourceName = "nginx-service";
        private const string SimpleService =
            "apiVersion: v1\nkind: Service\nmetadata:\n  name: nginx-service\nspec:\n  selector:\n    app.kubernetes.io/name: nginx\n  ports:\n    - protocol: TCP\n      port: 80\n      targetPort: 9376";

        private const string SimpleConfigMapResourceName = "game-demo";
        private const string SimpleConfigMap =
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: game-demo\ndata:\n  player_initial_lives: '3'\n  ui_properties_file_name: 'user-interface.properties'\n  game.properties: |\n    enemy.types=aliens,monsters\n    player.maximum-lives=5\n  user-interface.properties: |\n    color.good=purple\n    color.bad=yellow\n    allow.textmode=true";

        private const string SimpleConfigMap2ResourceName = "game-demo2";
        private const string SimpleConfigMap2 =
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: game-demo2\ndata:\n  player_initial_lives: '1'\n  ui_properties_file_name: 'user-interface.properties'\n  game.properties: |\n    enemy.types=blobs,foxes\n    player.maximum-lives=10\n  user-interface.properties: |\n    color.good=orange\n    color.bad=pink\n    allow.textmode=false";
        string eksClientID;
        string eksSecretKey;
        string eksClusterEndpoint;
        string eksClusterCaCertificate;
        string eksClusterName;
        string awsVpcID;
        string awsSubnetID;
        string awsIamInstanceProfileName;
        string region;
        string eksClusterArn;
        string eksIamRolArn;

        protected override string KubernetesCloudProvider => "EKS";

        protected override async Task PreInitialise()
        {
            region = RegionRandomiser.GetARegion();
            await TestContext.Progress.WriteLineAsync($"Aws Region chosen: {region}");
        }

        protected override async Task InstallOptionalTools(InstallTools tools)
        {
            await tools.InstallAwsAuthenticator();
            await tools.InstallAwsCli();
        }

        protected override IEnumerable<string> ToolsToAddToPath(InstallTools tools)
        {
            return new List<string> { tools.AwsAuthenticatorExecutable, tools.AwsCliExecutable };
        }

        protected override void ExtractVariablesFromTerraformOutput(JObject jsonOutput)
        {
            eksClientID = jsonOutput["eks_client_id"]["value"].Value<string>();
            eksSecretKey = jsonOutput["eks_secret_key"]["value"].Value<string>();
            eksClusterEndpoint = jsonOutput["eks_cluster_endpoint"]["value"].Value<string>();
            eksClusterCaCertificate = jsonOutput["eks_cluster_ca_certificate"]["value"].Value<string>();
            eksClusterName = jsonOutput["eks_cluster_name"]["value"].Value<string>();
            eksClusterArn = jsonOutput["eks_cluster_arn"]["value"].Value<string>();
            eksIamRolArn = jsonOutput["eks_iam_role_arn"]["value"].Value<string>();
            awsVpcID = jsonOutput["aws_vpc_id"]["value"].Value<string>();
            awsSubnetID = jsonOutput["aws_subnet_id"]["value"].Value<string>();
            awsIamInstanceProfileName = jsonOutput["aws_iam_instance_profile_name"]["value"].Value<string>();
        }

        protected override Dictionary<string, string> GetEnvironmentVars()
        {
            return new Dictionary<string, string>
            {
                { "AWS_ACCESS_KEY_ID", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey) },
                { "AWS_SECRET_ACCESS_KEY", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey) },
                { "AWS_DEFAULT_REGION", region },
                { "TF_VAR_tests_source_dir", testFolder }
            };
        }

        [Test]
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void DeployRawYaml_WithRawYamlDeploymentScriptOrCommand_OutputShouldIndicateSuccessfulDeployment(bool runAsScript, bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(runAsScript, usePackage, SimpleDeploymentResource);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).ToArray();

            var scrubbedJson = AssertResourceCreatedAndGetJson(SimpleDeploymentResourceName);

            this.Assent(scrubbedJson, configuration: AssentConfiguration.Default);

            AssertObjectStatusMonitoringStarted(runAsScript, rawLogs, (SimpleDeploymentResourceType, SimpleDeploymentResourceName));

            var objectStatusUpdates = Log.Messages.GetServiceMessagesOfType("k8s-status");

            objectStatusUpdates.Where(m => m.Properties["status"] == "Successful").Should().HaveCount(5);

            rawLogs.Should().ContainSingle(m =>
                m.Contains("Resource status check completed successfully because all resources are deployed successfully"));
        }

        private static void AssertObjectStatusMonitoringStarted(bool runAsScript, string[] rawLogs, params (string Type, string Name)[] resources)
        {
            var resourceStatusCheckLog = runAsScript
                ? "Resource Status Check: Performing resource status checks on the following resources:"
                : "Resource Status Check: 1 new resources have been added:";
            var idx = Array.IndexOf(rawLogs, resourceStatusCheckLog);
            foreach (var (i, type, name) in resources.Select((t, i) => (i, t.Type, t.Name)))
            {
                rawLogs[idx + i + 1].Should().Be($" - {type}/{name} in namespace calamari-testing");
            }
        }

        private string AssertResourceCreatedAndGetJson(string resourceName)
        {
            var variableMessages = Log.Messages.GetServiceMessagesOfType("setVariable");

            var variableMessage =
                variableMessages.Should().ContainSingle(m => m.Properties["name"] == $"CustomResources({resourceName})")
                                .Subject;

            return KubernetesJsonResourceScrubber.ScrubRawJson(variableMessage.Properties["value"], p =>
                p.Name.Contains("Time") ||
                p.Name == "annotations" ||
                p.Name == "uid" ||
                p.Name == "conditions" ||
                p.Name == "resourceVersion" ||
                p.Name == "status" ||
                p.Name == "generation" ||
                p.Name.StartsWith("clusterIP"));
        }

        [Test]
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void DeployRawYaml_WithInvalidYaml_OutputShouldIndicateFailure(bool runAsScript, bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(runAsScript, usePackage, InvalidDeploymentResource, shouldSucceed: false);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).Where(m => !m.StartsWith("##octopus") && m != string.Empty).ToArray();

            var fileName = usePackage ? $"deployments{Path.DirectorySeparatorChar}{DeploymentFileName}" : DeploymentFileName;
            var initialErrorMessage =
                $"error: error parsing {fileName}: error converting YAML to JSON: yaml: line 7: could not find expected ':'";
            rawLogs.Should()
                   .ContainSingle(l => l == initialErrorMessage);
            var index = Array.IndexOf(rawLogs, initialErrorMessage);
            var logsToCompare = new List<string>(rawLogs);
            // We'll check the rest of the logs after the one above
            // as they should be the same for all cases (except those
            // filtered out or adjusted below).
            logsToCompare.RemoveRange(0, index+1);
            logsToCompare = logsToCompare.Select(l =>
                                         {
                                             // This log line is slightly different in the new command because
                                             // we apply the yaml in batches (even if there is only one file).
                                             return l.Replace("\"kubectl apply -o json\" returned invalid JSON:",
                                                 "\"kubectl apply -o json\" returned invalid JSON for Batch #0:");
                                         })
                                         .Select(l =>
                                         {
                                             // There was actually no clean-up process for custom resources
                                             // so the log line produced by the deployment script doesn't
                                             // make sense. The new command does not have that part of the
                                             // log line.
                                             return l.Replace(
                                                 "Custom resources will not be saved as output variables, and will not be automatically cleaned up.",
                                                 "Custom resources will not be saved as output variables.");
                                         })
                                         .Where(l =>
                                         {
                                             // These log lines are in the old deployment script but the script
                                             // doesn't actually do the things that the logs describe and so they
                                             // aren't in the new command.
                                             return l != "The previous custom resources were not removed." &&
                                                 l != "The deployment process failed. The resources created by this step will be passed to \"kubectl describe\" and logged below.";
                                         })
                                         .Where(l =>
                                             {
                                               // These logs are printed after an error is caught but only for the new command
                                               return l != "Adding journal entry:" && !l.StartsWith("<Deployment Id=");
                                             })
                                         .ToList();

            this.Assent(string.Join('\n', logsToCompare), configuration: AssentConfiguration.Default);
        }

        [Test]
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void DeployRawYaml_WithYamlThatWillNotSucceed_OutputShouldIndicateFailure(bool runAsScript, bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(runAsScript, usePackage, FailToDeploymentResource, shouldSucceed: false);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).ToArray();

            var scrubbedJson = AssertResourceCreatedAndGetJson(SimpleDeploymentResourceName);

            this.Assent(scrubbedJson, configuration: AssentConfiguration.Default);

            AssertObjectStatusMonitoringStarted(runAsScript, rawLogs, (SimpleDeploymentResourceType, SimpleDeploymentResourceName));

            rawLogs.Should().ContainSingle(l =>
                l ==
                "Resource status check terminated because the timeout has been reached but some resources are still in progress");
        }

        [Test]
        public void DeployRawYaml_WithMultipleYamlFilesSpecifiedByGlobPatterns_YamlFilesAreAppliedInCorrectBatches()
        {
            SetVariablesToAuthoriseWithAmazonAccount();

            SetVariablesForKubernetesResourceStatusCheck(30);

            SetVariablesForRawYamlCommand();

            string CreatePackageWithMultipleYamlFiles(string directory)
            {
                var packageToPackage = CreatePackageWithFiles(ResourcePackageFileName, directory,
                    ("deployments", DeploymentFileName, SimpleDeploymentResource),
                    (Path.Combine("deployments", "subfolder"), DeploymentFileName2, SimpleDeploymentResource2),
                    ("services", ServiceFileName, SimpleService),
                    ("services", "EmptyYamlFile.yml", ""),
                    ("configmaps", ConfigMapFileName, SimpleConfigMap),
                    ("configmaps", ConfigMapFileName2, SimpleConfigMap2),
                    (Path.Combine("configmaps","subfolder"), "InvalidJSONNotUsed.yml", InvalidDeploymentResource));
                variables.Set("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName", $"deployments/**/*;services/{ServiceFileName};configmaps/*.yml");
                return packageToPackage;
            }

            ExecuteCommandAndVerifyResult(KubernetesApplyRawYamlCommand.Name, CreatePackageWithMultipleYamlFiles);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).Where(l => !l.StartsWith("##octopus")).ToArray();

            // We take the logs starting from when Calamari starts applying batches
            // to when the last k8s resource is created and compare them in an assent test.
            var startIndex = Array.FindIndex(rawLogs, l => l.StartsWith("Applying Batch #1"));
            var endIndex =
                Array.FindLastIndex(rawLogs, l => l == "Resource Status Check: 2 new resources have been added:") + 2;
            var assentLogs = rawLogs.Skip(startIndex)
                                    .Take(endIndex + 1 - startIndex)
                                    .Where(l => !l.StartsWith("##octopus")).ToArray();
            var batch3Index = Array.FindIndex(assentLogs, l => l.StartsWith("Applying Batch #3"));

            // In this case the two config maps have been loaded in reverse order
            // This can happen as Directory.EnumerateFiles() does not behave the
            // same on all platforms.
            // We'll flip them back the right way before performing the Assent Test.
            if (assentLogs[batch3Index + 1].Contains("myapp-configmap1.yml"))
            {
                var configMap1Idx = batch3Index + 1;
                var configMap2Idx = Array.FindIndex(assentLogs, l => l.Contains("myapp-configmap2.yml"));
                var endIdx = Array.FindLastIndex(assentLogs, l => l == "Created Resources:") - 1;
                InPlaceSwap(assentLogs, configMap1Idx, configMap2Idx, endIdx);
            }

            // We need to replace the backslash with forward slash because
            // the slash comes out differently on windows machines.
            var assentString = string.Join('\n', assentLogs).Replace("\\", "/");
            this.Assent(assentString, configuration: AssentConfiguration.DefaultWithPostfix("ApplyingBatches"));

            var resources = new[]
            {
                (Name: SimpleDeploymentResourceName, Label: "Deployment1"),
                (Name: SimpleDeployment2ResourceName,Label: "Deployment2"),
                (Name: SimpleServiceResourceName, Label: "Service1"),
                (Name: SimpleConfigMapResourceName, Label: "ConfigMap1"),
                (Name: SimpleConfigMap2ResourceName, Label: "ConfigMap3")
            };

            var statusMessages = Log.Messages.GetServiceMessagesOfType("k8s-status");

            foreach (var (name, label) in resources)
            {
                // Check that each resource was created and the appropriate setvariable service message was created.
                var resource = AssertResourceCreatedAndGetJson(name);
                this.Assent(resource, configuration: AssentConfiguration.DefaultWithPostfix(label));

                // Check that each deployed resource has a "Successful" status reported.
                statusMessages.Should().Contain(m => m.Properties["name"] == name && m.Properties["status"] == "Successful");
            }

            rawLogs.Should().ContainSingle(m =>
                m.Contains("Resource status check completed successfully because all resources are deployed successfully"));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void AuthorisingWithAmazonAccount(bool runAsScript)
        {
            SetVariablesToAuthoriseWithAmazonAccount();

            if (runAsScript)
            {
                DeployWithKubectlTestScriptAndVerifyResult();
            }
            else
            {
                ExecuteCommandAndVerifyResult(TestableKubernetesDeploymentCommand.Name);
            }
        }

        [Test]
        public void UnreachableK8Cluster_ShouldExecuteTargetScript()
        {
            const string account = "eks_account";
            const string certificateAuthority = "myauthority";
            const string unreachableClusterEndpoint = "https://example.kubernetes.com";

            variables.Set(SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(KubernetesSpecialVariables.ClusterUrl, unreachableClusterEndpoint);
            variables.Set(KubernetesSpecialVariables.EksClusterName, eksClusterName);
            variables.Set("Octopus.Action.Aws.Region", region);
            variables.Set("Octopus.Action.AwsAccount.Variable", account);
            variables.Set($"{account}.AccessKey", eksClientID);
            variables.Set($"{account}.SecretKey", eksSecretKey);
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", eksClusterCaCertificate);

            DeployWithNonKubectlTestScriptAndVerifyResult();
        }

        [Test]
        public void UsingEc2Instance()
        {
            var terraformWorkingFolder = InitialiseTerraformWorkingFolder("terraform_working/EC2", "KubernetesFixtures/Terraform/EC2");

            var env = new Dictionary<string, string>
            {
                { "TF_VAR_iam_role_arn", eksIamRolArn },
                { "TF_VAR_cluster_name", eksClusterName },
                { "TF_VAR_aws_vpc_id", awsVpcID },
                { "TF_VAR_aws_subnet_id", awsSubnetID },
                { "TF_VAR_aws_iam_instance_profile_name", awsIamInstanceProfileName },
                { "TF_VAR_aws_region", region }
            };

            RunTerraformInternal(terraformWorkingFolder, env, "init");
            try
            {
                // The actual tests are run via EC2/test.sh which executes the tests in
                // KubernetesContextScriptWrapperLiveFixtureForAmazon.cs
                RunTerraformInternal(terraformWorkingFolder, env, "apply", "-auto-approve");
            }
            finally
            {
                RunTerraformDestroy(terraformWorkingFolder, env);
            }
        }

        [Test]
        public void DiscoverKubernetesClusterWithEnvironmentVariableCredentialsAndIamRole()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, eksClientID);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, eksSecretKey);

                var authenticationDetails = new AwsAuthenticationDetails
                {
                    Type = "Aws",
                    Credentials = new AwsCredentials { Type = "worker" },
                    Role = new AwsAssumedRole
                    {
                        Type = "assumeRole",
                        Arn = eksIamRolArn
                    },
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
                        { "awsUseWorkerCredentials", bool.TrueString },
                        { "awsAssumeRole", bool.TrueString },
                        { "awsAssumeRoleArn", eksIamRolArn },
                    };

                DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                    serviceMessageProperties);
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }

        [Test]
        public void DiscoverKubernetesClusterWithEnvironmentVariableCredentialsAndNoIamRole()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, eksClientID);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, eksSecretKey);

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
                        { "awsUseWorkerCredentials", bool.TrueString },
                        { "awsAssumeRole", bool.FalseString }
                    };

                DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                    serviceMessageProperties);
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }

        [Test]
        public void DiscoverKubernetesClusterWithAwsAccountCredentialsAndNoIamRole()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = eksClientID,
                        SecretKey = eksSecretKey
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
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
                { "octopusAccountIdOrName", "Accounts-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsUseWorkerCredentials", bool.FalseString },
                { "awsAssumeRole", bool.FalseString }
            };

            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails, serviceMessageProperties);
        }

        [Test]
        public void DiscoverKubernetesClusterWithAwsAccountCredentialsAndIamRole()
        {
            const int sessionDuration = 900;
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = eksClientID,
                        SecretKey = eksSecretKey
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
                Role = new AwsAssumedRole
                {
                    Type = "assumeRole",
                    Arn = eksIamRolArn,
                    SessionName = "ThisIsASessionName",
                    SessionDuration = sessionDuration
                },
                Regions = new []{region}
            };

            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", eksClusterArn },
                { "clusterName", eksClusterName },
                { "clusterUrl", eksClusterEndpoint },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                { "octopusAccountIdOrName", "Accounts-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsUseWorkerCredentials", bool.FalseString },
                { "awsAssumeRole", bool.TrueString },
                { "awsAssumeRoleArn", eksIamRolArn },
                { "awsAssumeRoleSession", "ThisIsASessionName" },
                { "awsAssumeRoleSessionDurationSeconds", sessionDuration.ToString() }
            };

            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails, serviceMessageProperties);
        }

        [Test]
        public void DiscoverKubernetesClusterWithNoValidCredentials()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, null);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, null);

                var authenticationDetails = new AwsAuthenticationDetails
                {
                    Type = "Aws",
                    Credentials = new AwsCredentials { Type = "worker" },
                    Role = new AwsAssumedRole { Type = "noAssumedRole" },
                    Regions = new []{region}
                };

                DoDiscovery(authenticationDetails);

                Log.ServiceMessages.Should().NotContain(m =>
                    m.Name == KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName);

                Log.Messages.Should().NotContain(m => m.Level == InMemoryLog.Level.Error);

                Log.StandardError.Should().BeEmpty();

                Log.Messages.Should()
                    .ContainSingle(m =>
                        m.Level == InMemoryLog.Level.Warn &&
                        m.FormattedMessage ==
                        "Unable to authorise credentials, see verbose log for details.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }

        [Test]
        public void DiscoverKubernetesClusterWithInvalidAccountCredentials()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = "abcdefg",
                        SecretKey = null
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
                Role = new AwsAssumedRole { Type = "noAssumedRole" },
                Regions = new []{region}
            };

            DoDiscovery(authenticationDetails);

            Log.ServiceMessages.Should().NotContain(m =>
                m.Name == KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName);

            Log.MessagesErrorFormatted.Should().BeEmpty();
            Log.StandardError.Should().BeEmpty();

            Log.Messages.Should()
               .ContainSingle(m =>
                   m.Level == InMemoryLog.Level.Warn &&
                   m.FormattedMessage ==
                   "Unable to authorise credentials, see verbose log for details.");
        }

        private void SetupAndRunKubernetesRawYamlDeployment(bool runAsScript, bool usePackage, string resource, bool shouldSucceed = true)
        {
            SetVariablesToAuthoriseWithAmazonAccount();

            SetVariablesForKubernetesResourceStatusCheck(shouldSucceed ? 30 : 5);

            SetVariablesForRawYamlCommand();

            if (runAsScript)
            {
                DeployWithDeploymentScriptAndVerifyResult(usePackage
                        ? CreateAddPackageFunc(resource)
                        : CreateAddCustomResourceFileFunc(resource),
                    shouldSucceed);
            }
            else
            {
                ExecuteCommandAndVerifyResult(KubernetesApplyRawYamlCommand.Name,
                    usePackage
                        ? CreateAddPackageFunc(resource)
                        : CreateAddCustomResourceFileFunc(resource),
                    shouldSucceed);
            }
        }

        private void SetVariablesToAuthoriseWithAmazonAccount()
        {
            const string account = "eks_account";
            const string certificateAuthority = "myauthority";

            variables.Set(SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(KubernetesSpecialVariables.ClusterUrl, eksClusterEndpoint);
            variables.Set(KubernetesSpecialVariables.EksClusterName, eksClusterName);
            variables.Set("Octopus.Action.Aws.Region", region);
            variables.Set("Octopus.Action.AwsAccount.Variable", account);
            variables.Set($"{account}.AccessKey", eksClientID);
            variables.Set($"{account}.SecretKey", eksSecretKey);
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", eksClusterCaCertificate);
        }

        private void SetVariablesForRawYamlCommand()
        {
            variables.Set("Octopus.Action.KubernetesContainers.Namespace", "nginx");
            variables.Set("Octopus.Action.Package.JsonConfigurationVariablesTargets", "**/*.{yml,yaml}");

            variables.AddFeatureToggles(FeatureToggle.MultiGlobPathsForRawYamlFeatureToggle);
        }

        private void SetVariablesForKubernetesResourceStatusCheck(int timeout)
        {
            variables.Set("Octopus.Action.Kubernetes.ResourceStatusCheck", "True");
            variables.Set("Octopus.Action.KubernetesContainers.DeploymentWait", "NoWait");
            variables.Set("Octopus.Action.Kubernetes.DeploymentTimeout", timeout.ToString());
        }

        private static string CreateResourceYamlFile(string directory, string fileName, string content)
        {
            var pathToCustomResource = Path.Combine(directory, fileName);
            File.WriteAllText(pathToCustomResource, content);
            return pathToCustomResource;
        }

        private Func<string,string> CreateAddCustomResourceFileFunc(string yamlContent)
        {
            return directory =>
            {
                CreateResourceYamlFile(directory, DeploymentFileName, yamlContent);
                variables.Set("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName", DeploymentFileName);
                return null;
            };
        }

        private Func<string,string> CreateAddPackageFunc(string yamlContent)
        {
            return directory =>
            {
                var pathInPackage = Path.Combine("deployments", DeploymentFileName);
                var pathToPackage = CreatePackageWithFiles(ResourcePackageFileName, directory,
                    ("deployments", DeploymentFileName, yamlContent));
                variables.Set("Octopus.Action.KubernetesContainers.CustomResourceYamlFileName", pathInPackage);
                return pathToPackage;
            };
        }

        private string CreatePackageWithFiles(string packageFileName, string currentDirectory,
            params (string directory, string fileName, string content)[] files)
        {
            var pathToPackage = Path.Combine(currentDirectory, packageFileName);
            using (var archive = ZipArchive.Create())
            {
                var readStreams = new List<IDisposable>();
                foreach (var (directory, fileName, content) in files)
                {
                    var pathInPackage = Path.Combine(directory, fileName);
                    var readStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    readStreams.Add(readStream);
                    archive.AddEntry(pathInPackage, readStream);
                }

                using (var writeStream = File.OpenWrite(pathToPackage))
                {
                    archive.SaveTo(writeStream, CompressionType.Deflate);
                }

                foreach (var readStream in readStreams)
                {
                    readStream.Dispose();
                }
            }

            return pathToPackage;
        }

        private void InPlaceSwap(string[] array, int section1StartIndex, int section2StartIndex, int endIndex)
        {
            var length = endIndex + 1 - section1StartIndex;
            var section2TempIndex = section2StartIndex - section1StartIndex;
            var temp = new string[length];
            Array.Copy(array, section1StartIndex, temp, 0, length);
            Array.Copy(temp, section2TempIndex, array, section1StartIndex, temp.Length - section2TempIndex);
            Array.Copy(temp, 0, array, section1StartIndex + temp.Length - section2TempIndex, section2TempIndex);
        }
    }
}
#endif