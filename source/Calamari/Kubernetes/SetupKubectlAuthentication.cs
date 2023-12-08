using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Aws.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

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
            }

            var outputKubeConfig = variables.GetFlag(SpecialVariables.OutputKubeConfig);
            if (outputKubeConfig)
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "view");
            }

            return new CommandResult(string.Empty, 0);
        }

        bool TrySetupContext(string kubeConfig, string @namespace, string accountType)
        {
            if (accountType == AccountTypes.AzureServicePrincipal || accountType == AccountTypes.AzureOidc)
            {
                var azureCli = new AzureCli(log, commandLineRunner, workingDirectory, environmentVars);
                var kubeloginCli = new KubeLogin(log, commandLineRunner, workingDirectory, environmentVars);
                var azureAuth = new AzureKubernetesServicesAuth(azureCli, kubectl, kubeloginCli, variables);

                if (!azureAuth.TryConfigure(@namespace, kubeConfig))
                    return false;
            }
            else if (accountType == AccountTypes.GoogleCloudAccount || variables.GetFlag(Deployment.SpecialVariables.Action.GoogleCloud.UseVmServiceAccount))
            {
                var gcloudCli = new GCloud(log, commandLineRunner, fileSystem, workingDirectory, environmentVars);
                var gkeGcloudAuthPlugin = new GkeGcloudAuthPlugin(log, commandLineRunner, workingDirectory, environmentVars);
                var gcloudAuth = new GoogleKubernetesEngineAuth(gcloudCli, gkeGcloudAuthPlugin, kubectl, variables, log);

                if (!gcloudAuth.TryConfigure(@namespace))
                    return false;
            }
            else
            {
                var clusterUrl = variables.Get(SpecialVariables.ClusterUrl);
                if (string.IsNullOrEmpty(clusterUrl))
                {
                    log.Error("Kubernetes cluster URL is missing");
                    return false;
                }
                const string user = "octouser";
                const string cluster = "octocluster";
                const string context = "octocontext";

                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");

                if (variables.GetFlag(SpecialVariables.SkipTlsVerification))
                {
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify=true");
                } 
                else if (variables.IsSet(SpecialVariables.CertificateAuthorityPath))
                {
                    var serverCertPath = variables.Get(SpecialVariables.CertificateAuthorityPath);
                    if (!fileSystem.FileExists(serverCertPath))
                    {
                        log.Error("Certificate authority file not found");
                        return false;
                    }
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--certificate-authority={serverCertPath}");
                }
                else if (variables.IsSet(SpecialVariables.CertificateAuthority))
                {
                    var certificateAuthority = variables.Get(SpecialVariables.CertificateAuthority);
                    var serverCertPem = variables.Get(SpecialVariables.CertificatePem(certificateAuthority));
                    if (string.IsNullOrEmpty(serverCertPem))
                    {
                        log.Error("Kubernetes server certificate does not include the certificate data");
                        return false;
                    }

                    var authorityData = Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem));
                    log.AddValueToRedact(authorityData, "<data>");
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set", $"clusters.{cluster}.certificate-authority-data", authorityData);
                }
                else if(variables.IsSet(SpecialVariables.SkipTlsVerification))
                {
                    var skipTlsVerification = variables.GetFlag(SpecialVariables.SkipTlsVerification) ? "true" : "false";
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
                }
                
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-context", context, $"--user={user}", $"--cluster={cluster}", $"--namespace={@namespace}");
                kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", context);

                if (variables.IsSet(SpecialVariables.PodServiceAccountTokenPath))
                {
                    var podServiceAccountTokenPath = variables.Get(SpecialVariables.PodServiceAccountTokenPath);
                    if (!fileSystem.FileExists(podServiceAccountTokenPath))
                    {
                        log.Error("Pod service token file not found");
                        return false;
                    }

                    var podServiceAccountToken = fileSystem.ReadFile(podServiceAccountTokenPath);
                    if (string.IsNullOrEmpty(podServiceAccountToken))
                    {
                        log.Error("Pod service token file is empty");
                        return false;
                    }

                    log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
                    log.AddValueToRedact(podServiceAccountToken, "<token>");
                    kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={podServiceAccountToken}");
                }
                else if (accountType == AccountTypes.Token)
                {
                    var token = variables.Get(Deployment.SpecialVariables.Account.Token);
                    if (string.IsNullOrEmpty(token))
                    {
                        log.Error("Kubernetes authentication Token is missing");
                        return false;
                    }

                    SetupContextForToken(@namespace, token, clusterUrl, user);
                }
                else if (accountType == AccountTypes.UsernamePassword)
                {
                    SetupContextForUsernamePassword(user);
                }
                else if (accountType == AccountTypes.AmazonWebServicesAccount || variables.GetFlag(AwsSpecialVariables.Authentication.UseInstanceRole) || accountType == AccountTypes.AmazonWebServicesOidcAccount)
                {
                    var awsCli = new AwsCli(log, commandLineRunner, workingDirectory, environmentVars);
                    var awsAuth = new AwsCliAuth(awsCli, kubectl, variables, environmentVars, log);

                    if (!awsAuth.TryConfigure(@namespace, clusterUrl, user))
                        return false;
                }
                else if (variables.IsSet(SpecialVariables.ClientCertificate))
                {
                    var clientCert = variables.Get(SpecialVariables.ClientCertificate);
                    var clientCertPem = variables.Get(SpecialVariables.CertificatePem(clientCert));
                    var clientCertKey = variables.Get(SpecialVariables.PrivateKeyPem(clientCert));

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
                else if (string.IsNullOrEmpty(accountType))
                {
                    log.Error($"Kubernetes account type or certificate is missing");
                    return false;
                }
                else
                {
                    log.Error($"Account Type {accountType} is currently not valid for kubectl contexts");
                    return false;
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
            var username = variables.Get(Deployment.SpecialVariables.Account.Username);
            var password = variables.Get(Deployment.SpecialVariables.Account.Password);
            if (password != null)
            {
                log.AddValueToRedact(password, "<password>");
            }
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--username={username}", $"--password={password}");
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
    }
}