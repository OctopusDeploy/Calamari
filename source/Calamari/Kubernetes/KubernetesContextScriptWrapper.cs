using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

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

        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority + 1;

        /// <summary>
        /// One of these fields must be present for a k8s step
        /// </summary>
        public bool IsEnabled(ScriptSyntax syntax)
        {
            var hasClusterUrl = !string.IsNullOrEmpty(variables.Get(SpecialVariables.ClusterUrl));
            var hasClusterName = !string.IsNullOrEmpty(variables.Get(SpecialVariables.AksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.EksClusterName)) || !string.IsNullOrEmpty(variables.Get(SpecialVariables.GkeClusterName));
            return hasClusterUrl || hasClusterName;
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

            try
            {
                var result = setupKubectlAuthentication.Execute(accountType);

                if (result.ExitCode != 0)
                {
                    return result;
                }
            }
            catch (CommandLineException)
            {
                return new CommandResult(String.Empty, 1);
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
            string az;
            string aws;
            string gcloud;
            Dictionary<string, string> redactMap = new Dictionary<string, string>();

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

                var kubectlCli = new Kubectl(variables, kubectl, log, commandLineRunner, workingDirectory, environmentVars, redactMap);
                if (!kubectlCli.TrySetKubectl())
                {
                    return errorResult;
                }

                kubectl = kubectlCli.ExecutableLocation;

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
                    ExecuteKubectlCommand("config", "view");
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

                var isUsingGoogleCloudAuth = accountType == "GoogleCloudAccount" || useVmServiceAccount;
                var isUsingAzureServicePrincipalAuth = accountType == "AzureServicePrincipal";

                if (!isUsingAzureServicePrincipalAuth && !isUsingGoogleCloudAuth && string.IsNullOrEmpty(clusterUrl))
                {
                    log.Error("Kubernetes cluster URL is missing");
                    return false;
                }

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

                if (isUsingAzureServicePrincipalAuth)
                {
                    if (!TrySetAz())
                    {
                        log.Error("Could not find az. Make sure az is on the PATH.");
                        return false;
                    }

                    ConfigureAzAccount();

                    SetupContextForAzureServicePrincipal(kubeConfig, @namespace);
                }
                else if (isUsingGoogleCloudAuth)
                {
                    if (!TrySetGcloud())
                    {
                        log.Error("Could not find gcloud. Make sure gcloud is on the PATH.");
                        return false;
                    }

                    ConfigureGcloudAccount(useVmServiceAccount);
                    SetupContextForGoogleCloudAccount(@namespace);
                }
                else
                {
                    const string user = "octouser";
                    const string cluster = "octocluster";
                    const string context = "octocontext";
                    if (isUsingPodServiceAccount)
                    {
                        SetupContextUsingPodServiceAccount(@namespace, cluster, clusterUrl, serverCert,
                                                           skipTlsVerification,
                                                           serverCertPath,
                                                           context,
                                                           user,
                                                           podServiceAccountToken);
                    }
                    else
                    {
                        ExecuteKubectlCommand("config",
                                              "set-cluster",
                                              cluster,
                                              $"--server={clusterUrl}");

                        ExecuteKubectlCommand("config",
                                              "set-context",
                                              context,
                                              $"--user={user}",
                                              $"--cluster={cluster}",
                                              $"--namespace={@namespace}");

                        ExecuteKubectlCommand("config",
                                              "use-context",
                                              context);

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
                            redactMap[clientCertKeyEncoded] = "<data>";
                            redactMap[clientCertPemEncoded] = "<data>";
                            ExecuteKubectlCommand("config",
                                                  "set",
                                                  $"users.{user}.client-certificate-data",
                                                  clientCertPemEncoded);
                            ExecuteKubectlCommand("config",
                                                  "set",
                                                  $"users.{user}.client-key-data",
                                                  clientCertKeyEncoded);
                        }

                        if (!string.IsNullOrEmpty(certificateAuthority))
                        {
                            if (string.IsNullOrEmpty(serverCertPem))
                            {
                                log.Error("Kubernetes server certificate does not include the certificate data");
                                return false;
                            }

                            var authorityData = Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem));
                            redactMap[authorityData] = "<data>";
                            ExecuteKubectlCommand("config",
                                                  "set",
                                                  $"clusters.{cluster}.certificate-authority-data",
                                                  authorityData);
                        }
                        else
                        {
                            ExecuteKubectlCommand("config",
                                                  "set-cluster",
                                                  cluster,
                                                  $"--insecure-skip-tls-verify={skipTlsVerification}");
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
                redactMap[token] = "<token>";
                log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Token");
                ExecuteKubectlCommand("config",
                                      "set-credentials",
                                      user,
                                      $"--token={token}");
            }

            void SetupContextForUsernamePassword(string user)
            {
                var username = variables.Get("Octopus.Account.Username");
                var password = variables.Get("Octopus.Account.Password");
                redactMap[password] = "<password>";
                ExecuteKubectlCommand("config",
                                      "set-credentials",
                                      user,
                                      $"--username={username}",
                                      $"--password={password}");
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
                var awsEksTokenCommand = ExecuteCommandAndReturnOutput(aws,
                                                                       "eks",
                                                                       "get-token",
                                                                       $"--cluster-name={clusterName}",
                                                                       $"--region={region}")
                    .FirstOrDefault();

                return JObject.Parse(awsEksTokenCommand).SelectToken("apiVersion").ToString();
            }

            void SetKubeConfigAuthenticationToAwsCli(string user, string clusterName, string region, string apiVersion)
            {
                ExecuteKubectlCommand("config",
                                      "set-credentials",
                                      user,
                                      "--exec-command=aws",
                                      "--exec-arg=eks",
                                      "--exec-arg=get-token",
                                      $"--exec-arg=--cluster-name={clusterName}",
                                      $"--exec-arg=--region={region}",
                                      $"--exec-api-version={apiVersion}");
            }

            void SetKubeConfigAuthenticationToAwsIAm(string user, string clusterName)
            {
                var kubectlVersion = TrySetKubectlVersion();
                var apiVersion = kubectlVersion.Some() && kubectlVersion.Value > new SemanticVersion("1.23.6")
                    ? "client.authentication.k8s.io/v1beta1"
                    : "client.authentication.k8s.io/v1alpha1";

                ExecuteKubectlCommand("config",
                                      "set-credentials",
                                      user,
                                      "--exec-command=aws-iam-authenticator",
                                      $"--exec-api-version={apiVersion}",
                                      "--exec-arg=token",
                                      "--exec-arg=-i",
                                      $"--exec-arg={clusterName}");
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
                ExecuteKubectlCommand("config",
                                      "set-cluster",
                                      cluster,
                                      $"--server={clusterUrl}");

                if (string.IsNullOrEmpty(serverCert))
                {
                    ExecuteKubectlCommand("config",
                                          "set-cluster",
                                          cluster,
                                          $"--insecure-skip-tls-verify={skipTlsVerification}");
                }
                else
                {
                    ExecuteKubectlCommand("config",
                                          "set-cluster",
                                          cluster,
                                          $"--certificate-authority={serverCertPath}");
                }

                ExecuteKubectlCommand("config",
                                      "set-context",
                                      context,
                                      $"--user={user}",
                                      $"--cluster={cluster}",
                                      $"--namespace={@namespace}");
                ExecuteKubectlCommand("config",
                                      "use-context",
                                      context);

                log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
                redactMap[podServiceAccountToken] = "<token>";
                ExecuteKubectlCommand("config",
                                      "set-credentials",
                                      user,
                                      $"--token={podServiceAccountToken}");
            }

            void SetupContextForAzureServicePrincipal(string kubeConfig, string @namespace)
            {
                var azureResourceGroup = variables.Get("Octopus.Action.Kubernetes.AksClusterResourceGroup");
                var azureCluster = variables.Get(SpecialVariables.AksClusterName);
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
                    $"\"{kubeConfig}\"",
                    "--overwrite-existing"
                });
                if (azureAdmin)
                {
                    arguments.Add("--admin");
                    azureCluster += "-admin";
                }

                ExecuteCommand(az, arguments.ToArray());

                ExecuteKubectlCommand("config",
                                      "set-context",
                                      azureCluster,
                                      $"--namespace={@namespace}");
            }

            void SetupContextForGoogleCloudAccount(string @namespace)
            {
                var gkeClusterName = variables.Get(SpecialVariables.GkeClusterName);
                log.Info($"Creating kubectl context to GKE Cluster called {gkeClusterName} (namespace {@namespace}) using a Google Cloud Account");

                var arguments = new List<string>(new[]
                {
                    "container",
                    "clusters",
                    "get-credentials",
                    gkeClusterName
                });

                var region = variables.Get("Octopus.Action.GoogleCloud.Region");
                var zone = variables.Get("Octopus.Action.GoogleCloud.Zone");
                if (!string.IsNullOrWhiteSpace(zone))
                {
                    arguments.Add($"--zone={zone}");
                } 
                else if (!string.IsNullOrWhiteSpace(region))
                {
                    arguments.Add($"--region={region}");
                }
                else
                {
                    throw new ArgumentException("Either zone or region must be defined.");
                }

                ExecuteCommand(gcloud, arguments.ToArray());
                ExecuteKubectlCommand("config",
                                      "set-context",
                                      "--current",
                                      $"--namespace={@namespace}");
            }

            bool TrySetAz()
            {
                az = CalamariEnvironment.IsRunningOnWindows
                    ? ExecuteCommandAndReturnOutput("where", "az.cmd").FirstOrDefault()
                    : ExecuteCommandAndReturnOutput("which", "az").FirstOrDefault();

                if (string.IsNullOrEmpty(az))
                {
                    return false;
                }

                az = az.Trim();

                return true;
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

            bool TrySetGcloud()
            {
                gcloud = CalamariEnvironment.IsRunningOnWindows
                    ? ExecuteCommandAndReturnOutput("where", "gcloud.cmd").FirstOrDefault()
                    : ExecuteCommandAndReturnOutput("which", "gcloud").FirstOrDefault();

                if (string.IsNullOrEmpty(gcloud))
                {
                    return false;
                }

                gcloud = gcloud.Trim();
                return true;
            }

            bool CreateNamespace(string @namespace)
            {
                if (TryExecuteCommandWithVerboseLoggingOnly("get", "namespace", @namespace))
                    return true;

                return TryExecuteCommandWithVerboseLoggingOnly("create", "namespace", @namespace);
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
                TryExecuteCommand(az,
                               "cloud",
                               "set",
                               "--name",
                               GetAzEnvironment());

                log.Verbose("Azure CLI: Authenticating with Service Principal");

                var subscriptionId = variables.Get("Octopus.Action.Azure.SubscriptionId");
                var tenantId = variables.Get("Octopus.Action.Azure.TenantId");
                var clientId = variables.Get("Octopus.Action.Azure.ClientId");
                var password = variables.Get("Octopus.Action.Azure.Password");

                ExecuteCommand(az,
                               "login",
                               "--service-principal",
                               // Use the full argument with an '=' because of https://github.com/Azure/azure-cli/issues/12105
                               $"--username=\"{clientId}\"",
                               $"--password=\"{password}\"",
                               $"--tenant=\"{tenantId}\"");

                log.Verbose($"Azure CLI: Setting active subscription to {subscriptionId}");
                ExecuteCommand(az,
                               "account",
                               "set",
                               "--subscription",
                               subscriptionId);

                log.Info("Successfully authenticated with the Azure CLI");
            }

            void ConfigureGcloudAccount(bool useVmServiceAccount)
            {
                var project = variables.Get("Octopus.Action.GoogleCloud.Project") ?? string.Empty;
                var region = variables.Get("Octopus.Action.GoogleCloud.Region") ?? string.Empty;
                var zone = variables.Get("Octopus.Action.GoogleCloud.Zone") ?? string.Empty;
                if (!string.IsNullOrEmpty(project))
                {
                    environmentVars.Add("CLOUDSDK_CORE_PROJECT", project);
                }
                if (!string.IsNullOrEmpty(region))
                {
                    environmentVars.Add("CLOUDSDK_COMPUTE_REGION", region);
                }
                if (!string.IsNullOrEmpty(zone))
                {
                    environmentVars.Add("CLOUDSDK_COMPUTE_ZONE", zone);
                }

                if (!useVmServiceAccount)
                {
                    var accountVariable = variables.Get("Octopus.Action.GoogleCloudAccount.Variable");
                    var jsonKey = variables.Get($"{accountVariable}.JsonKey");
                    if (string.IsNullOrEmpty(accountVariable) || string.IsNullOrEmpty(jsonKey))
                    {
                        jsonKey = variables.Get("Octopus.Action.GoogleCloudAccount.JsonKey");
                    }

                    if (jsonKey == null)
                    {
                        log.Error("Failed to authenticate with gcloud. Key file is empty.");
                        return;
                    }

                    log.Verbose("Authenticating to gcloud with key file");
                    var bytes = Convert.FromBase64String(jsonKey);
                    using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, "gcpJsonKey.json")))
                    {
                        File.WriteAllBytes(keyFile.FilePath, bytes);
                        ExecuteCommand(gcloud,
                                       "auth",
                                       "activate-service-account",
                                       $"--key-file=\"{keyFile.FilePath}\"");

                    }

                    log.Verbose("Successfully authenticated with gcloud");
                }
                else
                {
                    log.Verbose("Bypassing authentication with gcloud");
                }

                if (variables.GetFlag("Octopus.Action.GoogleCloud.ImpersonateServiceAccount"))
                {
                    var impersonationEmails = variables.Get("Octopus.Action.GoogleCloud.ServiceAccountEmails");
                    if (!string.IsNullOrEmpty(impersonationEmails))
                        environmentVars.Add("CLOUDSDK_AUTH_IMPERSONATE_SERVICE_ACCOUNT", impersonationEmails);
                }

            }

            string ConfigureCliExecution()
            {
                var kubeConfig = Path.Combine(workingDirectory, "kubectl-octo.yml");

                // create an empty file, to suppress kubectl errors about the file missing
                File.WriteAllText(kubeConfig, string.Empty);

                environmentVars.Add("KUBECONFIG", kubeConfig);

                if (scriptSyntax == ScriptSyntax.Bash)
                {
                    ExecuteCommand("chmod", "u=rw,g=,o=", $"\"{kubeConfig}\"");
                }

                log.Verbose($"Temporary kubectl config set to {kubeConfig}");

                return kubeConfig;
            }

            Maybe<SemanticVersion> TrySetKubectlVersion()
            {
                var kubectlVersionOutput = ExecuteCommandAndReturnOutput(kubectl, "version", "--client", "--output=json");
                var kubeCtlVersionJson = string.Join(" ", kubectlVersionOutput);
                try
                {
                    var clientVersion = JsonConvert.DeserializeAnonymousType(kubeCtlVersionJson, new { clientVersion = new { gitVersion = "1.0.0" } });
                    var kubectlVersionString = clientVersion?.clientVersion?.gitVersion?.TrimStart('v');
                    if (kubectlVersionString != null)
                    {
                        return Maybe<SemanticVersion>.Some(new SemanticVersion(kubectlVersionString));
                    }
                }
                catch (Exception e)
                {
                    log.Verbose($"Unable to determine kubectl version. Failed with error message: {e.Message}");
                }

                return Maybe<SemanticVersion>.None;
            }

            void ExecuteCommand(string executable, params string[] arguments)
            {
                ExecuteCommand(new CommandLineInvocation(executable, arguments)).VerifySuccess();
            }

            bool TryExecuteCommand(string executable, params string[] arguments)
            {
                return ExecuteCommand(new CommandLineInvocation(executable, arguments)).ExitCode == 0;
            }

            void ExecuteKubectlCommand(params string[] arguments)
            {
                ExecuteCommand(new CommandLineInvocation(kubectl, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).VerifySuccess();
            }

            bool TryExecuteKubectlCommand(params string[] arguments)
            {
                return ExecuteCommand(new CommandLineInvocation(kubectl, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
            }

            bool TryExecuteCommandWithVerboseLoggingOnly(params string[] arguments)
            {
                return ExecuteCommandWithVerboseLoggingOnly(new CommandLineInvocation(kubectl, arguments.Concat(new[] { "--request-timeout=1m" }).ToArray())).ExitCode == 0;
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
                commandString = redactMap.Aggregate(commandString, (current, pair) => current.Replace(pair.Key, pair.Value));
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
                commandString = redactMap.Aggregate(commandString, (current, pair) => current.Replace(pair.Key, pair.Value));
                log.Verbose(commandString);

                var result = commandLineRunner.Execute(invocation);

                captureCommandOutput.Messages.ForEach(message => log.Verbose(message.Text));

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
                    ? captureCommandOutput.Messages.Where(m => m.Level == Level.Info).Select(m => m.Text).ToArray()
                    : Enumerable.Empty<string>();
            }
        }
    }
}
