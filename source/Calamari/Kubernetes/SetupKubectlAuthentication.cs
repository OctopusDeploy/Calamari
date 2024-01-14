#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
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

        public CommandResult Execute()
        {
            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
            {
                environmentVars[proxyVariable.Key] = proxyVariable.Value;
            }

            try
            {
                kubectl.SetKubectl();
                var @namespace = GetNamespaceOrDefault();
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

        string GetNamespaceOrDefault()
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
            else if (variables.IsSet(SpecialVariables.ClusterUrl)) // Most other auth mechanisms require some manual commands to configure
            {
                var clusterUrl = variables.Get(SpecialVariables.ClusterUrl);
                const string user = "octouser";
                const string cluster = "octocluster";
                const string context = "octocontext";

                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");
                SetCertAuthority(cluster);
                kubectl.ExecuteCommandAndAssertSuccess("config",
                                                       "set-context",
                                                       context,
                                                       $"--user={user}",
                                                       $"--cluster={cluster}",
                                                       $"--namespace={@namespace}");
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
                else if (accountType == AccountTypes.AmazonWebServicesAccount || variables.GetFlag(AwsSpecialVariables.Authentication.UseInstanceRole) || accountType == AccountTypes.AmazonWebServicesOidcAccount)
                {
                    SetupAwsContext(@namespace, clusterUrl, user);
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
            else if (HasAmbientKubeContext())
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

            kubectl.ExecuteCommandAndAssertSuccess("config",
                                                   "set-credentials",
                                                   user,
                                                   $"--username={username}",
                                                   $"--password={password}");
        }

        void SetupAwsContext(string @namespace, string clusterUrl, string user)
        {
            var awsCli = new AwsCli(log, commandLineRunner, workingDirectory, environmentVars);
            var awsAuth = new AwsCliAuth(awsCli, kubectl, variables, environmentVars, log);

            awsAuth.Configure(@namespace, clusterUrl, user);
        }

        void CreateNamespace(string @namespace)
        {
            if (kubectl.ExecuteCommandWithVerboseLoggingOnly("get", "namespace", @namespace).ExitCode == 0)
                return;

            if (kubectl.ExecuteCommandWithVerboseLoggingOnly("create", "namespace", @namespace).ExitCode != 0)
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
    }
}
#endif