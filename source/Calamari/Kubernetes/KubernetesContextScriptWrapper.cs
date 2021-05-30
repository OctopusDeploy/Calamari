using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public class KubernetesContextScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICalamariEmbeddedResources embeddedResources;

        public KubernetesContextScriptWrapper(IVariables variables, ILog log, ICalamariEmbeddedResources embeddedResources, ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.log = log;
            this.fileSystem = fileSystem;
            this.embeddedResources = embeddedResources;
        }

        public int Priority => ScriptWrapperPriorities.ToolConfigPriority;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public bool IsEnabled(ScriptSyntax syntax)
        {
            return (!string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)));
        }

        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
                                           ScriptSyntax scriptSyntax,
                                           ICommandLineRunner commandLineRunner,
                                           Dictionary<string, string> environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            if (environmentVars == null)
            {
                environmentVars = new Dictionary<string, string>();
            }

            var setupKubectlAuthentication = new SetupKubectlAuthentication(variables,
                                                                            log,
                                                                            scriptSyntax,
                                                                            commandLineRunner,
                                                                            environmentVars,
                                                                            workingDirectory);
            var accountType = variables.Get("Octopus.Account.AccountType");
            var result = setupKubectlAuthentication.Execute(accountType);

            if (result.ExitCode != 0)
            {
                return result;
            }

            if (scriptSyntax == ScriptSyntax.PowerShell && accountType == "AzureServicePrincipal")
            {
                variables.Set("OctopusKubernetesTargetScript", $"{script.File}");
                variables.Set("OctopusKubernetesTargetScriptParameters", script.Parameters);

                using (var contextScriptFile = new TemporaryFile(CreateContextScriptFile(workingDirectory)))
                {
                    return NextWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), scriptSyntax, commandLineRunner, environmentVars);
                }
            }

            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }

        string CreateContextScriptFile(string workingDirectory)
        {
            const string contextFile = "AzurePowershellContext.ps1";
            var contextScriptFile = Path.Combine(workingDirectory, $"Octopus.{contextFile}");
            var contextScript = embeddedResources.GetEmbeddedResourceText(Assembly.GetExecutingAssembly(), $"Calamari.Kubernetes.Scripts.{contextFile}");
            fileSystem.OverwriteFile(contextScriptFile, contextScript);
            return contextScriptFile;
        }

        class SetupKubectlAuthentication
        {
            readonly IVariables variables;
            readonly ILog log;
            readonly ScriptSyntax scriptSyntax;
            readonly ICommandLineRunner commandLineRunner;
            readonly Dictionary<string, string> environmentVars;
            readonly string workingDirectory;
            string kubectl;

            public SetupKubectlAuthentication(IVariables variables,
                                              ILog log,
                                              ScriptSyntax scriptSyntax,
                                              ICommandLineRunner commandLineRunner,
                                              Dictionary<string, string> environmentVars,
                                              string workingDirectory)
            {
                this.variables = variables;
                this.log = log;
                this.scriptSyntax = scriptSyntax;
                this.commandLineRunner = commandLineRunner;
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

                var kubeConfig = ConfigureCliExecution();

                if (!TryGetKubectl())
                {
                    return errorResult;
                }

                var @namespace = variables.Get("Octopus.Action.Kubernetes.Namespace");
                if (string.IsNullOrEmpty(@namespace))
                {
                    log.Verbose("No namespace provided. Using default");
                    @namespace = "default";
                }

                if (!TrySetupContext(kubeConfig, @namespace, accountType))
                {
                    return errorResult;
                }

                CreateNamespace(@namespace);

                var outputKubeConfig = variables.GetFlag("Octopus.Action.Kubernetes.OutputKubeConfig");
                if (outputKubeConfig)
                {
                    ExecuteKubectlCommand("config", "view");
                }

                return new CommandResult(string.Empty, 0);
            }

            bool TrySetupContext(string kubeConfig, string @namespace, string accountType)
            {
                var clusterUrl = variables.Get("Octopus.Action.Kubernetes.ClusterUrl");
                var clientCert = variables.Get("Octopus.Action.Kubernetes.ClientCertificate");
                var eksUseInstanceRole = variables.GetFlag("Octopus.Action.AwsAccount.UseInstanceRole");
                var podServiceAccountTokenPath = variables.Get("Octopus.Action.Kubernetes.PodServiceAccountTokenPath");
                var serverCertPath = variables.Get("Octopus.Action.Kubernetes.CertificateAuthorityPath");
                var isUsingPodServiceAccount = false;
                var skipTlsVerification = variables.GetFlag("Octopus.Action.Kubernetes.SkipTlsVerification") ? "true" : "false";

                if (accountType != "AzureServicePrincipal" && string.IsNullOrEmpty(clusterUrl))
                {
                    log.Error("Kubernetes cluster URL is missing");
                    return false;
                }

                string podServiceAccountToken = null;
                string serverCert = null;
                if (string.IsNullOrEmpty(accountType) && string.IsNullOrEmpty(clientCert) && eksUseInstanceRole == false)
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

                if (accountType == "AzureServicePrincipal")
                {
                    ConfigureAzAccount();

                    var azureResourceGroup = variables.Get("Octopus.Action.Kubernetes.AksClusterResourceGroup");
                    var azureCluster = variables.Get("Octopus.Action.Kubernetes.AksClusterName");
                    var azureAdmin = variables.GetFlag("Octopus.Action.Kubernetes.AksAdminLogin");
                    log.Info($"Creating kubectl context to AKS Cluster in resource group {azureResourceGroup} called {azureCluster} (namespace {@namespace}) using a AzureServicePrincipal");

                    var arguments = new List<string>(new[]
                    {
                        "aks",
                        "get-credentials",
                        "--resource-group",
                        azureResourceGroup,
                        "--name",
                        azureCluster,
                        "--file",
                        kubeConfig,
                        "--overwrite-existing"
                    });
                    if (azureAdmin)
                    {
                        arguments.Add("--admin");
                        azureCluster += "-admin";
                    }

                    ExecuteCommand("az", LogType.Info, arguments.ToArray());

                    ExecuteKubectlCommand("config",
                                          "set-context",
                                          azureCluster,
                                          $"--namespace={@namespace}");
                }
                else if (isUsingPodServiceAccount)
                {
                    ExecuteKubectlCommand("config",
                                          "set-cluster",
                                          "octocluster",
                                          $"--server={clusterUrl}");

                    if (string.IsNullOrEmpty(serverCert))
                    {
                        ExecuteKubectlCommand("config",
                                              "set-cluster",
                                              "octocluster",
                                              $"--insecure-skip-tls-verify={skipTlsVerification}");
                    }
                    else
                    {
                        ExecuteKubectlCommand("config",
                                              "set-cluster",
                                              "octocluster",
                                              $"--certificate-authority={serverCertPath}");
                    }

                    ExecuteKubectlCommand("config",
                                          "set-context",
                                          "octocontext",
                                          "--user=octouser",
                                          "--cluster=octocluster",
                                          $"--namespace={@namespace}");
                    ExecuteKubectlCommand("config",
                                          "use-context",
                                          "octocontext");

                    log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
                    ExecuteKubectlCommand(LogType.None,
                                          "config",
                                          "set-credentials",
                                          "octouser",
                                          $"--token={podServiceAccountToken}");
                }
                else
                {
                    ExecuteKubectlCommand("config",
                                          "set-cluster",
                                          "octocluster",
                                          $"--server={clusterUrl}");

                    ExecuteKubectlCommand("config",
                                          "set-context",
                                          "octocontext",
                                          "--user=octouser",
                                          "--cluster=octocluster",
                                          $"--namespace={@namespace}");

                    ExecuteKubectlCommand("config",
                                          "use-context",
                                          "octocontext");

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
                        ExecuteKubectlCommand(LogType.None,
                                              "config",
                                              "set",
                                              "users.octouser.client-certificate-data",
                                              clientCertPemEncoded);
                        ExecuteKubectlCommand(LogType.None,
                                              "config",
                                              "set",
                                              "users.octouser.client-key-data",
                                              clientCertKeyEncoded);
                    }

                    if (!string.IsNullOrEmpty(certificateAuthority))
                    {
                        if (string.IsNullOrEmpty(serverCertPem))
                        {
                            log.Error("Kubernetes server certificate does not include the certificate data");
                            return false;
                        }

                        ExecuteKubectlCommand(LogType.None,
                                              "config",
                                              "set",
                                              "clusters.octocluster.certificate-authority-data",
                                              Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem)));
                    }
                    else
                    {
                        ExecuteKubectlCommand("config",
                                              "set-cluster",
                                              "octocluster",
                                              $"--insecure-skip-tls-verify={skipTlsVerification}");
                    }

                    switch (accountType)
                    {
                        case "Token":
                        {
                            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Token");

                            var token = variables.Get("Octopus.Account.Token");
                            if (string.IsNullOrEmpty(token))
                            {
                                log.Error("Kubernetes authentication Token is missing");
                                return false;
                            }

                            ExecuteKubectlCommand(LogType.None,
                                                  "config",
                                                  "set-credentials",
                                                  "octouser",
                                                  $"--token={token}");
                            break;
                        }
                        case "UsernamePassword":
                        {
                            var username = variables.Get("Octopus.Account.Username");
                            var password = variables.Get("Octopus.Account.Password");
                            ExecuteKubectlCommand(LogType.None,
                                                  "config",
                                                  "set-credentials",
                                                  "octouser",
                                                  $"--username={username}",
                                                  $"--password={password}");
                            break;
                        }
                        default:
                        {
                            if (accountType == "AmazonWebServicesAccount" || eksUseInstanceRole)
                            {
                                /*
                                kubectl doesn't yet support exec authentication
                                https://github.com/kubernetes/kubernetes/issues/64751
                                so build this manually
                                */
                                var clusterName = variables.Get("Octopus.Action.Kubernetes.EksClusterName");
                                log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using EKS cluster name {clusterName}");

                                /*
                                The call to set-cluster above will create a file with empty users. We need to call
                                set-cluster first, because if we try to add the exec user first, set-cluster will
                                delete those settings. So we now delete the users line (the last line of the yaml file)
                                and add our own.
                                */

                                var kubeConfigYaml = File.ReadAllText(kubeConfig);
                                // https://docs.aws.amazon.com/eks/latest/userguide/create-kubeconfig.html
                                kubeConfigYaml = kubeConfigYaml.Replace("users: []",
                                                                        $@"users:
- name: aws
  user:
    exec:
      apiVersion: client.authentication.k8s.io/v1alpha1
      command: aws-iam-authenticator
      args:
        - ""token""
                - ""-i""
                - ""{clusterName}""");

                                File.WriteAllText(kubeConfig, kubeConfigYaml);
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

                return true;
            }

            void CreateNamespace(string @namespace)
            {
                if (ExecuteCommand(kubectl,
                                   LogType.None,
                                   "get",
                                   "namespace",
                                   @namespace)
                        .ExitCode
                    != 0)
                {
                    ExecuteKubectlCommand("create", "namespace", @namespace);
                }
            }

            string GetAzEnvironment()
            {
                return variables.Get("Octopus.Action.Azure.Environment") ?? "AzureCloud";
            }

            void ConfigureAzAccount()
            {
                var disableAzureCli = variables.GetFlag("OctopusDisableAzureCLI");

                if (disableAzureCli)
                {
                    return;
                }

                environmentVars.Add("AZURE_CONFIG_DIR", Path.Combine(workingDirectory, "azure-cli"));
                ExecuteCommand("az",
                               LogType.Info,
                               "cloud",
                               "set",
                               "--name",
                               GetAzEnvironment());

                log.Verbose("Azure CLI: Authenticating with Service Principal");

                var subscriptionId = variables.Get("Octopus.Action.Azure.SubscriptionId");
                var tenantId = variables.Get("Octopus.Action.Azure.TenantId");
                var clientId = variables.Get("Octopus.Action.Azure.ClientId");
                var password = variables.Get("Octopus.Action.Azure.Password");

                ExecuteCommand("az",
                               LogType.Info,
                               "login",
                               "--service-principal",
                               // Use the full argument with an '=' because of https://github.com/Azure/azure-cli/issues/12105
                               $"--username=\"{clientId}\"",
                               $"--password=\"{password}\"",
                               $"--tenant=\"{tenantId}\"");

                log.Verbose($"Azure CLI: Setting active subscription to {subscriptionId}");
                ExecuteCommand("az",
                               LogType.Info,
                               "account",
                               "set",
                               "--subscription",
                               subscriptionId);

                log.Info("Successfully authenticated with the Azure CLI");
            }

            string ConfigureCliExecution()
            {
                var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

                // create an empty file, to suppress kubectl errors about the file missing
                File.WriteAllText(kubeConfig, string.Empty);

                environmentVars.Add("KUBECONFIG", kubeConfig);

                if (scriptSyntax == ScriptSyntax.Bash)
                {
                    ExecuteCommand("chmod", LogType.None, "u=rw,g=,o=", kubeConfig).VerifySuccess();
                }

                log.Verbose($"Temporary kubectl config set to {kubeConfig}");

                return kubeConfig;
            }

            bool TryGetKubectl()
            {
                kubectl = variables.Get("Octopus.Action.Kubernetes.CustomKubectlExecutable");
                if (string.IsNullOrEmpty(kubectl))
                {
                    kubectl = "kubectl";
                }
                else if (!File.Exists(kubectl))
                {
                    log.Error($"The custom kubectl location of {kubectl} does not exist. See https://g.octopushq.com/KubernetesTarget for more information.");
                    return false;
                }

                if (ExecuteKubectlCommand("version", "--client", "--short").ExitCode == 0)
                {
                    return true;
                }

                log.Error($"Could not find $Kubectl_Exe. Make sure {kubectl} is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information.");
                return false;
            }

            CommandResult ExecuteCommand(string executable, LogType logType, params string[] arguments)
            {
                return ExecuteCommand(new CommandLineInvocation(executable, arguments), logType);
            }

            CommandResult ExecuteKubectlCommand(LogType logType, params string[] arguments)
            {
                return ExecuteCommand(new CommandLineInvocation(kubectl, arguments), logType);
            }

            CommandResult ExecuteKubectlCommand(params string[] arguments)
            {
                return ExecuteKubectlCommand(LogType.Info, arguments);
            }

            CommandResult ExecuteCommand(CommandLineInvocation invocation, LogType logType)
            {
                invocation.EnvironmentVars = environmentVars;
                invocation.WorkingDirectory = workingDirectory;
                invocation.OutputAsVerbose = logType == LogType.Verbose;
                invocation.OutputToLog = logType == LogType.Info;

                if (logType != LogType.None)
                {
                    log.Verbose(invocation.ToString());
                }

                var result = commandLineRunner.Execute(invocation);

                return result;
            }

            enum LogType
            {
                None,
                Verbose,
                Info
            }
        }
    }
}
