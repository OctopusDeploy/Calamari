using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes
{
    public class SetupKubectlAuthentication
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        readonly ICalamariFileSystem fileSystem;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;
        string aws;

        public SetupKubectlAuthentication(IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            IKubectl kubectl,
            ICalamariFileSystem fileSystem,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.fileSystem = fileSystem;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public CommandResult Execute()
        {
            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }

            try
            {
                kubectl.SetKubectl();
                var @namespace = DetermineNamespace();
                SetupContext(@namespace);
                CreateNamespace(@namespace);

                var outputKubeConfig = variables.GetFlag(SpecialVariables.OutputKubeConfig);
                if (outputKubeConfig)
                {
                    kubectl.ExecuteCommandAndAssertSuccess("config", "view");
                }
            }
            catch (KubectlException e)
            {
                log.Error(e.Message);
                return new CommandResult(string.Empty, 1);
            }

            return new CommandResult(string.Empty, 0);
        }

        string DetermineNamespace()
        {
            var @namespace = variables.Get(SpecialVariables.Namespace);
            if (!string.IsNullOrEmpty(@namespace))
                return @namespace;

            log.Verbose("No namespace provided. Using default");
            return "default";
        }

        void SetupContext(string @namespace)
        {
            var kubeConfig = CreateKubectlConfig();
            var accountType = variables.Get(Deployment.SpecialVariables.Account.AccountType);
            if (accountType == AccountTypes.AzureServicePrincipal || accountType == AccountTypes.AzureOidc)
            {
                SetupAzureContext(@namespace, kubeConfig);
            }
            else if (accountType == AccountTypes.GoogleCloudAccount || variables.GetFlag(Deployment.SpecialVariables.Action.GoogleCloud.UseVmServiceAccount))
            {
                SetupGCloudContext(@namespace);
            }
            else if(variables.IsSet(SpecialVariables.ClusterUrl)) // Most other auth mechanisms require some manual commands to configure
            {
                var clusterUrl = variables.Get(SpecialVariables.ClusterUrl);
                const string user = "octouser";
                const string cluster = "octocluster";
                const string context = "octocontext";

                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");
                SetCertAuthority(cluster);
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-context", context, $"--user={user}", $"--cluster={cluster}", $"--namespace={@namespace}");
                kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", context);

                if (variables.IsSet(SpecialVariables.PodServiceAccountTokenPath))
                {
                    SetupPodServiceAccountTokenAuth(@namespace, clusterUrl, user);
                }
                else if (accountType == AccountTypes.Token)
                {
                    SetupContextForToken(@namespace, clusterUrl, user);
                }
                else if (accountType == AccountTypes.UsernamePassword)
                {
                    SetupContextForUsernamePassword(user);
                }
                else if (accountType == AccountTypes.AmazonWebServicesAccount || variables.GetFlag("Octopus.Action.AwsAccount.UseInstanceRole") || accountType == AccountTypes.AmazonWebServicesOidcAccount)
                {
                    SetupContextForAmazonServiceAccount(@namespace, clusterUrl, user);
                }
                else if (variables.IsSet(SpecialVariables.ClientCertificate))
                {
                    SetupContextForClientCertificate(user);
                }
                else if (string.IsNullOrEmpty(accountType))
                {
                    throw new KubectlException($"Kubernetes account type or certificate is missing");
                }
                else
                {
                    throw new KubectlException($"Account Type {accountType} is currently not valid for kubectl contexts");
                }
            }
            else if(HasAmbientKubeContext())
            {
                SetupAmbientContext(@namespace, kubeConfig);
                kubectl.DisableRequestTimeoutArgument();
            }
            else
            {
                throw new KubectlException($"Unable to configure Kubernetes authentication context. Please verify your target configuration.");
            }
        }

        void SetupPodServiceAccountTokenAuth(string @namespace, string clusterUrl, string user)
        {
            var podServiceAccountTokenPath = variables.Get(SpecialVariables.PodServiceAccountTokenPath);
            if (!fileSystem.FileExists(podServiceAccountTokenPath))
            {
                throw new KubectlException("Pod service token file not found");
            }

            var podServiceAccountToken = fileSystem.ReadFile(podServiceAccountTokenPath);
            if (string.IsNullOrEmpty(podServiceAccountToken))
            {
                throw new KubectlException("Pod service token file is empty");
            }

            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
            log.AddValueToRedact(podServiceAccountToken, "<token>");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={podServiceAccountToken}");
        }

        void SetupGCloudContext(string @namespace)
        {
            var gcloudCli = new GCloud(log,
                                       commandLineRunner,
                                       fileSystem,
                                       workingDirectory,
                                       environmentVars);
            var gkeGcloudAuthPlugin = new GkeGcloudAuthPlugin(log, commandLineRunner, workingDirectory, environmentVars);
            var gcloudAuth = new GoogleKubernetesEngineAuth(gcloudCli,
                                                            gkeGcloudAuthPlugin,
                                                            kubectl,
                                                            variables,
                                                            log);
            gcloudAuth.Configure(@namespace);
        }

        void SetupAzureContext(string @namespace, string kubeConfig)
        {
            var azureCli = new AzureCli(log, commandLineRunner, workingDirectory, environmentVars);
            var kubeloginCli = new KubeLogin(log, commandLineRunner, workingDirectory, environmentVars);
            var azureAuth = new AzureKubernetesServicesAuth(azureCli, kubectl, kubeloginCli, variables);
            azureAuth.Configure(@namespace, kubeConfig);
        }

        void SetupAmbientContext(string @namespace, string kubeConfig)
        {
            /*
            By default kubectl will look at which maps to the namespace the pod is running in.
            The POD_NAMESPACE environment variable allows this to be overridden,
            https://kubernetes.io/docs/reference/kubectl/#in-cluster-authentication-and-namespace-overrides
            */
            log.Verbose("Detected ambient cluster context. Assuming running inside the cluster");
            environmentVars.Add("POD_NAMESPACE", @namespace);

            // Cleanup the kubeconfig that we dont want to use
            fileSystem.DeleteFile(kubeConfig);
            environmentVars.Remove("KUBECONFIG");
        }

        bool HasAmbientKubeContext()
        {
            /*
             * kubectl looks for environment configuration to use ambient context provided by the cluster
             * https://kubernetes.io/docs/tasks/run-application/access-api-from-pod/#directly-accessing-the-rest-api
             */
            return fileSystem.FileExists("/var/run/secrets/kubernetes.io/serviceaccount/token")
                   && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"))
                   && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT"));
        }

        void SetupContextForClientCertificate(string user)
        {
            var clientCert = variables.Get(SpecialVariables.ClientCertificate);
            var clientCertPem = variables.Get(SpecialVariables.CertificatePem(clientCert));
            var clientCertKey = variables.Get(SpecialVariables.PrivateKeyPem(clientCert));

            if (string.IsNullOrEmpty(clientCertPem))
            {
                throw new KubectlException("Kubernetes client certificate does not include the certificate data");
            }

            if (string.IsNullOrEmpty(clientCertKey))
            {
                throw new KubectlException("Kubernetes client certificate does not include the private key data");
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

        void SetCertAuthority(string cluster)
        {
            if (variables.GetFlag(SpecialVariables.SkipTlsVerification))
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify=true");
            }
            else if (variables.IsSet(SpecialVariables.CertificateAuthorityPath))
            {
                var serverCertPath = variables.Get(SpecialVariables.CertificateAuthorityPath);
                if (!fileSystem.FileExists(serverCertPath))
                {
                    throw new KubectlException("Certificate authority file not found");
                }

                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--certificate-authority={serverCertPath}");
            }
            else if (variables.IsSet(SpecialVariables.CertificateAuthority))
            {
                var certificateAuthority = variables.Get(SpecialVariables.CertificateAuthority);
                var serverCertPem = variables.Get(SpecialVariables.CertificatePem(certificateAuthority));
                if (string.IsNullOrEmpty(serverCertPem))
                {
                    throw new KubectlException("Kubernetes server certificate does not include the certificate data");
                }

                var authorityData = Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem));
                log.AddValueToRedact(authorityData, "<data>");
                kubectl.ExecuteCommandAndAssertSuccess("config", "set", $"clusters.{cluster}.certificate-authority-data", authorityData);
            }
            else if (variables.IsSet(SpecialVariables.SkipTlsVerification))
            {
                var skipTlsVerification = variables.GetFlag(SpecialVariables.SkipTlsVerification) ? "true" : "false";
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
            }
        }

        void SetupContextForToken(string @namespace, string clusterUrl, string user)
        {
            var token = variables.Get(Deployment.SpecialVariables.Account.Token);
            if (string.IsNullOrEmpty(token))
            {
                throw new KubectlException("Kubernetes authentication Token is missing");
            }

            log.AddValueToRedact(token, "<token>");
            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Token");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={token}");
        }

        void SetupContextForUsernamePassword(string user)
        {
            var username = variables.Get(Deployment.SpecialVariables.Account.Username);
            var password = variables.Get(Deployment.SpecialVariables.Account.Password);
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
            var oidcJwt = variables.Get("Octopus.OpenIdConnect.Jwt");
            var arguments = new List<string>
            {
                "config",
                "set-credentials",
                user,
                "--exec-command=aws",
                "--exec-arg=eks",
                "--exec-arg=get-token",
                $"--exec-arg=--cluster-name={clusterName}",
                $"--exec-arg=--region={region}",
                $"--exec-api-version={apiVersion}"
            };
            
            if (!oidcJwt.IsNullOrEmpty())
            {
                arguments.Add($"--token={oidcJwt}");
            }
            
            kubectl.ExecuteCommandAndAssertSuccess(arguments.ToArray());
        }

        void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
        {
            var kubectlVersion = kubectl.GetVersion();
            var apiVersion = kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                ? "client.authentication.k8s.io/v1beta1"
                : "client.authentication.k8s.io/v1alpha1";

            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, "--exec-command=aws-iam-authenticator", $"--exec-api-version={apiVersion}", "--exec-arg=token", "--exec-arg=-i", $"--exec-arg={clusterName}");
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

        void CreateNamespace(string @namespace)
        {
            if (TryExecuteCommandWithVerboseLoggingOnly("get", "namespace", @namespace))
                return;

            if (!TryExecuteCommandWithVerboseLoggingOnly("create", "namespace", @namespace))
            {
                log.Verbose("Could not create namespace. Continuing on, as it may not be working directly with the target.");
            }
        }

        string CreateKubectlConfig()
        {
            var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

            // create an empty file, to suppress kubectl errors about the file missing
            fileSystem.WriteAllText(kubeConfig, string.Empty);

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