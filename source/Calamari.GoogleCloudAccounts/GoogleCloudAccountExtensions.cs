using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.GoogleCloudAccounts
{
        public class SetupGCloudAuthentication
        {
            readonly IVariables variables;
            readonly ILog log;
            readonly ICommandLineRunner commandLineRunner;
            readonly Dictionary<string, string> environmentVars;
            readonly string workingDirectory;
            readonly string jwtFilePath;
            readonly string jsonAuthFilePath;
            private string gcloud = String.Empty;

            public SetupGCloudAuthentication(IVariables variables,
                ILog log,
                ICommandLineRunner commandLineRunner,
                Dictionary<string, string> environmentVars,
                GcloudOAuthFileConfiguration oAuthConfiguration)
            {
                this.variables = variables;
                this.log = log;
                this.commandLineRunner = commandLineRunner;
                this.environmentVars = environmentVars;
                this.workingDirectory = oAuthConfiguration.WorkingDirectory;
                this.jwtFilePath = oAuthConfiguration.JwtFile.FilePath;
                this.jsonAuthFilePath = oAuthConfiguration.JsonAuthFile.FilePath;
            }

            public CommandResult Execute()
            {
                var errorResult = new CommandResult(string.Empty, 1);

                foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
                {
                    environmentVars[proxyVariable.Key] = proxyVariable.Value;
                }

                environmentVars.Add("CLOUDSDK_CORE_DISABLE_PROMPTS", "1");
                var gcloudConfigPath = Path.Combine(workingDirectory, "gcloud-cli");
                environmentVars.Add("CLOUDSDK_CONFIG", gcloudConfigPath);
                Directory.CreateDirectory(gcloudConfigPath);
                
                if (!TrySetGcloudExecutable())
                {
                    return errorResult;
                }

                log.Verbose($"Using gcloud from {gcloud}.");

                var useVmServiceAccount = variables.GetFlag("Octopus.Action.GoogleCloud.UseVMServiceAccount");
                string? impersonationEmails = null;
                if (variables.GetFlag("Octopus.Action.GoogleCloud.ImpersonateServiceAccount"))
                {
                    impersonationEmails = variables.Get("Octopus.Action.GoogleCloud.ServiceAccountEmails");
                }

                ConfigureGcloudEnvironmentVariables();

                if (!useVmServiceAccount)
                {
                    var accountVariable = variables.Get("Octopus.Action.GoogleCloudAccount.Variable");
                    var jsonKey = variables.Get($"{accountVariable}.JsonKey");
                    var jwtToken = variables.Get($"{accountVariable}.OpenIdConnect.Jwt");

                    if (!string.IsNullOrWhiteSpace(jsonKey))
                    {
                        if (!TryAuthenticateWithServiceAccount(jsonKey))
                        {
                            return errorResult;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(jwtToken))
                    {
                        if (!TryAuthenticateWithOidc(accountVariable, jwtToken, impersonationEmails))
                        {
                            return errorResult;
                        }
                    }
                    else
                    {
                        log.Error("Failed to authenticate with gcloud. Key file and JWT token are both empty.");
                        return errorResult;
                    }

                    log.Verbose("Successfully authenticated with gcloud");
                }
                else
                {
                    log.Verbose("Bypassing authentication with gcloud");
                }

                if (impersonationEmails != null)
                {
                    if (ExecuteCommand("config", "set", "auth/impersonate_service_account", impersonationEmails)
                        .ExitCode != 0)
                    {
                        log.Error("Failed to impersonate service account.");
                        return errorResult;
                    }
                    log.Verbose("Impersonation emails set.");
                }

                return new CommandResult(string.Empty, 0);
            }

            CommandResult ExecuteCommand(params string[] arguments)
            {
                if (gcloud == null)
                {
                    throw new Exception("gcloud is null");
                }

                var captureCommandOutput = new CaptureCommandOutput();
                var invocation = new CommandLineInvocation(gcloud, arguments)
                {
                    EnvironmentVars = environmentVars,
                    WorkingDirectory = workingDirectory,
                    OutputAsVerbose = false,
                    OutputToLog = false,
                    AdditionalInvocationOutputSink = captureCommandOutput
                };

                log.Verbose(invocation.ToString());

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
                        case Level.Verbose:
                            log.Verbose(message.Text);
                            break;
                        case Level.Error:
                            log.Error(message.Text);
                            break;
                    }
                }

                return result;
            }

            bool TrySetGcloudExecutable()
            {
                gcloud = variables.Get("Octopus.Action.GoogleCloud.CustomExecutable");
                if (!string.IsNullOrEmpty(gcloud))
                {
                    if (!File.Exists(gcloud))
                    {
                        log.Error($"The custom gcloud location of {gcloud} does not exist. Please make sure gcloud is installed in that location.");
                        return false;
                    }
                }
                else
                {
                    gcloud = CalamariEnvironment.IsRunningOnWindows
                        ? ExecuteCommandAndReturnOutput("where", "gcloud.cmd")
                        : ExecuteCommandAndReturnOutput("which", "gcloud");

                    if (string.IsNullOrEmpty(gcloud))
                    {
                        log.Error("Could not find gcloud. Make sure gcloud is on the PATH.");
                        return false;
                    }
                }

                return true;
            }

            void ConfigureGcloudEnvironmentVariables()
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
            }

            bool TryAuthenticateWithServiceAccount(string jsonKey)
            {
                log.Verbose("Authenticating to gcloud with key file");
                var bytes = Convert.FromBase64String(jsonKey);
                using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, Path.GetRandomFileName())))
                {
                    File.WriteAllBytes(keyFile.FilePath, bytes);
                    if (ExecuteCommand("auth", "activate-service-account", $"--key-file=\"{keyFile.FilePath}\"")
                            .ExitCode
                        != 0)
                    {
                        log.Error("Failed to authenticate with gcloud.");
                        return false;
                    }
                }

                return true;
            }

            bool TryAuthenticateWithOidc(string accountVariable, string jwtToken, string? impersonationEmails)
            {
                log.Verbose("Authenticating to gcloud with JWT token.");
                var serverUri = variables.Get("Octopus.Web.ServerUri");
                var audience = variables.Get($"{accountVariable}.Audience");

                if (serverUri == null)
                {
                    log.Error("Failed to authenticate with gcloud. ServerUri is empty.");
                    return false;
                }

                if (audience == null)
                {
                    log.Error("Failed to authenticate with gcloud. Audience is empty.");
                    return false;
                }

                File.WriteAllText(jwtFilePath, jwtToken);

                if (audience.Contains("iam.googleapis.com/"))
                {
                    audience = audience.Substring(audience.IndexOf("iam.googleapis.com/", StringComparison.Ordinal) + "iam.googleapis.com/".Length);
                }

                var createConfigResult = ExecuteCommand("iam",
                                                        "workload-identity-pools",
                                                        "create-cred-config",
                                                        audience,
                                                        $"--service-account={impersonationEmails}",
                                                        "--service-account-token-lifetime-seconds=3600",
                                                        "--subject-token-type=urn:ietf:params:oauth:token-type:jwt",
                                                        "--credential-source-type=text",
                                                        $"--credential-source-file={jwtFilePath}",
                                                        "--app-id-uri",
                                                        serverUri,
                                                        $"--output-file={jsonAuthFilePath}");
                if (createConfigResult.ExitCode != 0)
                {
                    log.Error("Failed to create credential config with gcloud.");
                    return false;
                }

                if (ExecuteCommand("auth",
                                   "login",
                                   $"--cred-file={jsonAuthFilePath}")
                        .ExitCode
                    != 0)
                {
                    log.Error("Failed to authenticate with gcloud.");
                    return false;
                }

                return true;
            }

            string? ExecuteCommandAndReturnOutput(string exe, params string[] arguments)
            {
                var captureCommandOutput = new CaptureCommandOutput();
                var envVars = Environment.GetEnvironmentVariables();
                // The environment variables are not always populated in upstream calling methods.
                // Here we just care about the path variable for Nix/Windows for the where/which calls.
                var envDict = new Dictionary<string, string>();
                foreach (var key in envVars.Keys)
                {
                    envDict.Add(key.ToString(), envVars[key].ToString());
                }

                var invocation = new CommandLineInvocation(exe, arguments)
                {
                    EnvironmentVars = envDict,
                    WorkingDirectory = workingDirectory,
                    OutputAsVerbose = false,
                    OutputToLog = false,
                    AdditionalInvocationOutputSink = captureCommandOutput
                };

                var result = commandLineRunner.Execute(invocation);

                return result.ExitCode == 0
                    ? String.Join(Environment.NewLine, captureCommandOutput.Messages.Where(m => m.Level == Level.Verbose).Select(m => m.Text).ToArray()).Trim()
                    : null;
            }
        }
}