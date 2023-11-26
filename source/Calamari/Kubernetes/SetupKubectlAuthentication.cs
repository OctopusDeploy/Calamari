using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes
{
    public class SetupKubectlAuthentication
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        private readonly Kubectl kubectl;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;
        string aws;

        public SetupKubectlAuthentication(IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            Kubectl kubectl,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public CommandResult Execute(string accountType)
        {
            var errorResult = new CommandResult(string.Empty, 1);

            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }

            var kubeConfig = CreateKubectlConfig();
            if (!kubectl.TrySetKubectl())
            {
                return errorResult;
            }

            var @namespace = variables.Get(SpecialVariables.Namespace);
            if (string.IsNullOrEmpty(@namespace))
            {
                log.Verbose("No namespace provided. Using default");
                @namespace = "default";
            }

            if (!TrySetupContext(kubeConfig, @namespace, accountType))
            {
                return errorResult;
            }

            if (!CreateNamespace(@namespace))
            {
                log.Verbose("Could not create namespace. Continuing on, as it may not be working directly with the target.");
            };

            var outputKubeConfig = variables.GetFlag(SpecialVariables.OutputKubeConfig);
            if (outputKubeConfig)
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "view");
            }

            return new CommandResult(string.Empty, 0);
        }

        bool TrySetupContext(string kubeConfig, string @namespace, string accountType)
        {
            var clusterUrl = variables.Get(SpecialVariables.ClusterUrl);
            var clientCert = variables.Get("Octopus.Action.Kubernetes.ClientCertificate");
            var eksUseInstanceRole = variables.GetFlag("Octopus.Action.AwsAccount.UseInstanceRole");
            var podServiceAccountTokenPath = variables.Get("Octopus.Action.Kubernetes.PodServiceAccountTokenPath");
            var serverCertPath = variables.Get("Octopus.Action.Kubernetes.CertificateAuthorityPath");
            var isUsingPodServiceAccount = false;
            var skipTlsVerification = variables.GetFlag(SpecialVariables.SkipTlsVerification) ? "true" : "false";
            var useVmServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.UseVMServiceAccount");


            string podServiceAccountToken = null;
            string serverCert = null;
            if (string.IsNullOrEmpty(accountType) && string.IsNullOrEmpty(clientCert) && !eksUseInstanceRole && !useVmServiceAccount)
            {
                if (string.IsNullOrEmpty(podServiceAccountTokenPath) && string.IsNullOrEmpty(serverCertPath))
                {
                    log.Error("Kubernetes account type or certificate is missing");
                    return false;
                }

                if (!string.IsNullOrEmpty(podServiceAccountTokenPath))
                {
                    if (File.Exists(podServiceAccountTokenPath))
                    {
                        podServiceAccountToken = File.ReadAllText(podServiceAccountTokenPath);
                        if (string.IsNullOrEmpty(podServiceAccountToken))
                        {
                            log.Error("Pod service token file is empty");
                            return false;
                        }

                        isUsingPodServiceAccount = true;
                    }
                    else
                    {
                        log.Error("Pod service token file not found");
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(serverCertPath))
                {
                    if (File.Exists(serverCertPath))
                    {
                        serverCert = File.ReadAllText(serverCertPath);
                    }
                    else
                    {
                        log.Error("Certificate authority file not found");
                        return false;
                    }
                }
            }

            if (accountType == "AzureServicePrincipal" ||  accountType == "AzureOidc")
            {
                var azureCli = new AzureCli(log, commandLineRunner, workingDirectory, environmentVars);
                var kubeloginCli = new KubeLogin(log, commandLineRunner, workingDirectory, environmentVars);
                var azureAuth = new AzureKubernetesServicesAuth(azureCli, kubectl, kubeloginCli, variables);

                if (!azureAuth.TryConfigure(@namespace, kubeConfig))
                    return false;
            }
            else if (accountType == "GoogleCloudAccount" || useVmServiceAccount)
            {
                var gcloudCli = new GCloud(log, commandLineRunner, workingDirectory, environmentVars);
                var gkeGcloudAuthPlugin = new GkeGcloudAuthPlugin(log, commandLineRunner, workingDirectory, environmentVars);
                var gcloudAuth = new GoogleKubernetesEngineAuth(gcloudCli, gkeGcloudAuthPlugin, kubectl, variables, log);

                if (!gcloudAuth.TryConfigure(useVmServiceAccount, @namespace))
                    return false;
            }
            else
            {
                if (string.IsNullOrEmpty(clusterUrl))
                {
                    log.Error("Kubernetes cluster URL is missing");
                    return false;
                }
                const string user = "octouser";
                const string cluster = "octocluster";
                const string context = "octocontext";
                
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-context", context, $"--user={user}", $"--cluster={cluster}", $"--namespace={@namespace}");
                kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", context);
                
                if (isUsingPodServiceAccount)
                {
                    if (string.IsNullOrEmpty(serverCert))
                    {
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
                    }
                    else
                    {
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--certificate-authority={serverCertPath}");
                    }

                    log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
                    log.AddValueToRedact(podServiceAccountToken, "<token>");
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={podServiceAccountToken}");
                }
                else
                {
                    var clientCertPem = variables.Get($"{clientCert}.CertificatePem");
                    var clientCertKey = variables.Get($"{clientCert}.PrivateKeyPem");
                    var certificateAuthority = variables.Get("Octopus.Action.Kubernetes.CertificateAuthority");
                    var serverCertPem = variables.Get($"{certificateAuthority}.CertificatePem");

                    if (!string.IsNullOrEmpty(clientCert))
                    {
                        if (string.IsNullOrEmpty(clientCertPem))
                        {
                            log.Error("Kubernetes client certificate does not include the certificate data");
                            return false;
                        }

                        if (string.IsNullOrEmpty(clientCertKey))
                        {
                            log.Error("Kubernetes client certificate does not include the private key data");
                            return false;
                        }

                        log.Verbose("Encoding client cert key");
                        var clientCertKeyEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientCertKey));
                        log.Verbose("Encoding client cert pem");
                        var clientCertPemEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientCertPem));

                        // Don't leak the private key in the logs
                        log.SetOutputVariable($"{clientCert}.PrivateKeyPemBase64", clientCertKeyEncoded, variables, true);
                        log.AddValueToRedact(clientCertKeyEncoded, "<data>");
                        log.AddValueToRedact(clientCertPemEncoded, "<data>");
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set", $"users.{user}.client-certificate-data", clientCertPemEncoded);
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set", $"users.{user}.client-key-data", clientCertKeyEncoded);
                    }

                    if (!string.IsNullOrEmpty(certificateAuthority))
                    {
                        if (string.IsNullOrEmpty(serverCertPem))
                        {
                            log.Error("Kubernetes server certificate does not include the certificate data");
                            return false;
                        }

                        var authorityData = Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem));
                        log.AddValueToRedact(authorityData, "<data>");
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set", $"clusters.{cluster}.certificate-authority-data", authorityData);
                    }
                    else
                    {
                        kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
                    }

                    switch (accountType)
                    {
                        case "Token":
                        {
                            var token = variables.Get("Octopus.Account.Token");
                            if (string.IsNullOrEmpty(token))
                            {
                                log.Error("Kubernetes authentication Token is missing");
                                return false;
                            }

                            SetupContextForToken(@namespace, token, clusterUrl, user);
                            break;
                        }
                        case "UsernamePassword":
                        {
                            SetupContextForUsernamePassword(user);
                            break;
                        }
                        default:
                        {
                            if (accountType == "AmazonWebServicesAccount" || eksUseInstanceRole)
                            {
                                SetupContextForAmazonServiceAccount(@namespace, clusterUrl, user);
                            }
                            else if (string.IsNullOrEmpty(clientCert))
                            {
                                log.Error($"Account Type {accountType} is currently not valid for kubectl contexts");
                                return false;
                            }

                            break;
                        }
                    }
                }
            }

            return true;
        }

        void SetupContextForToken(string @namespace, string token, string clusterUrl, string user)
        {
            log.AddValueToRedact(token, "<token>");
            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Token");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={token}");
        }

        void SetupContextForUsernamePassword(string user)
        {
            var username = variables.Get("Octopus.Account.Username");
            var password = variables.Get("Octopus.Account.Password");
            if (password != null)
            {
                log.AddValueToRedact(password, "<password>");
            }
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--username={username}", $"--password={password}");
        }

        void SetupContextForAmazonServiceAccount(string @namespace, string clusterUrl, string user)
        {
            var clusterName = variables.Get(SpecialVariables.EksClusterName);
            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

            if (TrySetKubeConfigAuthenticationToAwsCli(clusterName, clusterUrl, user))
            {
                return;
            }

            log.Verbose("Attempting to authenticate with aws-iam-authenticator");
            SetKubeConfigAuthenticationToAwsIAm(user, clusterName);
        }

        bool TrySetKubeConfigAuthenticationToAwsCli(string clusterName, string clusterUrl, string user)
        {
            log.Verbose("Attempting to authenticate with aws-cli");
            if (!TrySetAws())
            {
                log.Verbose("Could not find the aws cli, falling back to the aws-iam-authenticator.");
                return false;
            }

            try {
                var awsCliVersion = GetAwsCliVersion();
                var minimumAwsCliVersionForAuth = new SemanticVersion("1.16.156");
                if (awsCliVersion.CompareTo(minimumAwsCliVersionForAuth) > 0)
                {
                    var region = GetEksClusterRegion(clusterUrl);
                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        var apiVersion = GetEksClusterApiVersion(clusterName, region);
                        SetKubeConfigAuthenticationToAwsCli(user, clusterName, region, apiVersion);
                        return true;
                    }

                    log.Verbose("The EKS cluster Url specified should contain a valid aws region name");
                }

                log.Verbose($"aws cli version: {awsCliVersion} does not support the \"aws eks get-token\" command. Please update to a version later than 1.16.156");
            }
            catch (Exception e)
            {
                log.Verbose($"Unable to authenticate to {clusterUrl} using the aws cli. Failed with error message: {e.Message}");
            }

            return false;
        }

        string GetEksClusterRegion(string clusterUrl) => clusterUrl.Replace(".eks.amazonaws.com", "").Split('.').Last();

        SemanticVersion GetAwsCliVersion()
        {
            var awsCliCommandRes = ExecuteCommandAndReturnOutput(aws, "--version").FirstOrDefault();
            var awsCliVersionString = awsCliCommandRes.Split()
                                                      .FirstOrDefault(versions => versions.StartsWith("aws-cli"))
                                                      .Replace("aws-cli/", string.Empty);
            return new SemanticVersion(awsCliVersionString);
        }

        string GetEksClusterApiVersion(string clusterName, string region)
        {
            var logLines = ExecuteCommandAndReturnOutput(aws,
                "eks",
                "get-token",
                $"--cluster-name={clusterName}",
                $"--region={region}");
            var awsEksTokenCommand = string.Join("\n", logLines);
            return JObject.Parse(awsEksTokenCommand).SelectToken("apiVersion").ToString();
        }

        void SetKubeConfigAuthenticationToAwsCli(string user, string clusterName, string region, string apiVersion)
        {
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, "--exec-command=aws", "--exec-arg=eks", "--exec-arg=get-token", $"--exec-arg=--cluster-name={clusterName}", $"--exec-arg=--region={region}", $"--exec-api-version={apiVersion}");
        }

        void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
        {
            var kubectlVersion = kubectl.GetVersion();
            var apiVersion = kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                ? "client.authentication.k8s.io/v1beta1"
                : "client.authentication.k8s.io/v1alpha1";

            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, "--exec-command=aws-iam-authenticator", $"--exec-api-version={apiVersion}", "--exec-arg=token", "--exec-arg=-i", $"--exec-arg={clusterName}");
        }

        void SetupContextUsingPodServiceAccount(string @namespace,
            string cluster,
            string clusterUrl,
            string serverCert,
            string skipTlsVerification,
            string serverCertPath,
            string context,
            string user,
            string podServiceAccountToken)
        {
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");

            if (string.IsNullOrEmpty(serverCert))
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
            }
            else
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--certificate-authority={serverCertPath}");
            }

            kubectl.ExecuteCommandAndAssertSuccess("config", "set-context", context, $"--user={user}", $"--cluster={cluster}", $"--namespace={@namespace}");
            kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", context);

            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
            log.AddValueToRedact(podServiceAccountToken, "<token>");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={podServiceAccountToken}");
        }

        bool TrySetAws()
        {
            aws = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "aws.exe").FirstOrDefault()
                : ExecuteCommandAndReturnOutput("which", "aws").FirstOrDefault();

            if (string.IsNullOrEmpty(aws))
            {
                return false;
            }

            aws = aws.Trim();

            return true;
        }

        bool CreateNamespace(string @namespace)
        {
            if (TryExecuteCommandWithVerboseLoggingOnly("get", "namespace", @namespace))
                return true;

            return TryExecuteCommandWithVerboseLoggingOnly("create", "namespace", @namespace);
        }

        string CreateKubectlConfig()
        {
            var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

            // create an empty file, to suppress kubectl errors about the file missing
            File.WriteAllText(kubeConfig, string.Empty);

            environmentVars.Add("KUBECONFIG", kubeConfig);

            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                ExecuteCommand("chmod", "u=rw,g=,o=", $"\"{kubeConfig}\"");
            }

            log.Verbose($"Temporary kubectl config set to {kubeConfig}");

            return kubeConfig;
        }

        void ExecuteCommand(string executable, params string[] arguments)
        {
            ExecuteCommand(new CommandLineInvocation(executable, arguments)).VerifySuccess();
        }

        bool TryExecuteCommand(string executable, params string[] arguments)
        {
            return ExecuteCommand(new CommandLineInvocation(executable, arguments)).ExitCode == 0;
        }

        bool TryExecuteCommandWithVerboseLoggingOnly(params string[] arguments)
        {
            return ExecuteCommandWithVerboseLoggingOnly(new CommandLineInvocation(kubectl.ExecutableLocation, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
        }

        CommandResult ExecuteCommand(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = false;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            var commandString = invocation.ToString();
            log.Verbose(commandString);

            var result = commandLineRunner.Execute(invocation);

            foreach (var message in captureCommandOutput.Messages)
            {
                if (result.ExitCode == 0)
                {
                    log.Verbose(message.Text);
                    continue;
                }

                switch (message.Level)
                {
                    case Level.Info:
                        log.Verbose(message.Text);
                        break;
                    case Level.Error:
                        log.Error(message.Text);
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// This is a special case for when the invocation results in an error
        /// 1) but is to be expected as a valid scenario; and
        /// 2) we don't want to inform this at an error level when this happens.
        /// </summary>
        /// <param name="invocation"></param>
        /// <returns></returns>
        CommandResult ExecuteCommandWithVerboseLoggingOnly(CommandLineInvocation invocation)
        {
            invocation.EnvironmentVars = environmentVars;
            invocation.WorkingDirectory = workingDirectory;
            invocation.OutputAsVerbose = true;
            invocation.OutputToLog = false;

            var captureCommandOutput = new CaptureCommandOutput();
            invocation.AdditionalInvocationOutputSink = captureCommandOutput;

            var commandString = invocation.ToString();
            log.Verbose(commandString);

            var result = commandLineRunner.Execute(invocation);

            foreach (var message in captureCommandOutput.Messages)
            {
                log.Verbose(message.Text);
            }

            return result;
        }

        IEnumerable<string> ExecuteCommandAndReturnOutput(string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                EnvironmentVars = environmentVars,
                WorkingDirectory = workingDirectory,
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };

            var result = commandLineRunner.Execute(invocation);

            return result.ExitCode == 0
                ? captureCommandOutput.InfoLogs.ToArray()
                : Enumerable.Empty<string>();
        }
    }
}