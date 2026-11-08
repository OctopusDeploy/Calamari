using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Calamari.Build.Types;
using Newtonsoft.Json;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.AzureSignTool;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities;
using Polly;
using Polly.Retry;

namespace Calamari.Build.Utilities;

/// <summary>
/// This is copied from OctopusServer
/// </summary>
/// <param name="signingCertificatePath"></param>
/// <param name="azureKeyVaultSettings"></param>
/// <param name="jSignDockerImage"></param>
class Signing(
    string signingCertificatePath,
    AzureKeyVaultSettings azureKeyVaultSettings,
    string jSignDockerImage)
{
    static readonly string[] SigningTimestampUrls =
    [
        "http://timestamp.digicert.com?alg=sha256",
        "http://timestamp.comodoca.com"
    ];

    readonly ResiliencePipeline Retry = new ResiliencePipelineBuilder()
                                        .AddRetry(new RetryStrategyOptions
                                        {
                                            ShouldHandle = new PredicateBuilder().Handle<ProcessException>(),
                                            Delay = TimeSpan.FromSeconds(2),
                                            BackoffType = DelayBackoffType.Exponential,
                                            MaxDelay = TimeSpan.FromSeconds(60),
                                            MaxRetryAttempts = 10,
                                            OnRetry = async arguments =>
                                                      {
                                                          Log.ForContext("AttemptNumber", arguments.AttemptNumber)
                                                             .Information(arguments.Outcome.Exception, "Exception while attempting to sign files; retrying in {Duration}", arguments.RetryDelay);
                                                          await Task.CompletedTask;
                                                      }
                                        })
                                        .Build();

    public async Task Sign(AbsolutePath parentDirectory, IReadOnlyCollection<AbsolutePath> filesToSign, string signingCertificatePassword)
    {
        if (azureKeyVaultSettings.IsEmpty())
        {
            // The production certificate is not available, so we will use the self-signed development certificate.
            if (!OperatingSystem.IsWindows())
            {
                throw new NotImplementedException("Signing with a self-signed certificate is not supported on non-Windows platforms yet.");
            }

            SignWithDevelopmentSelfSignedCertOnWindows(filesToSign, signingCertificatePassword);
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            await SignWithProductionCertWithJSign(parentDirectory, filesToSign);
            return;
        }

        SignWithProductionCertWithAzureSignTool(filesToSign);
    }

    class RetryState
    {
        public int AttemptNumber;
    }

    /// <summary>
    /// Signs files using the self-signed development signing certificate.
    /// Used for local development only.
    /// Relies on the signtool.exe being available at the root directory and will only work on Windows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    void SignWithDevelopmentSelfSignedCertOnWindows(IReadOnlyCollection<AbsolutePath> filesToSign, string signingCertificatePassword)
    {
        Log.Information("Signing files using SignTool and the self-signed development code signing certificate");
        var signingTool = KnownPaths.RootDirectory / "signtool.exe";

        if (!File.Exists(signingTool))
        {
            throw new Exception($"The signing tool was expected to be at the path '{signingTool}' but wasn't available.");
        }

        if (!File.Exists(signingCertificatePath))
        {
            throw new Exception($"The code-signing certificate was not found at {signingCertificatePath}.");
        }

        Retry.Execute((state, ct) =>
                      {
                          //we want retries here, as the timestamp server can be flaky
                          var timestampUrl = GetTimestampUrl(state.AttemptNumber++);

                          Logging.InBlock($"Trying to sign and time stamp using {timestampUrl}", () =>
                                                                                                 {
                                                                                                     SignToolTasks.SignTool(settings => settings
                                                                                                                                        .SetFile(signingCertificatePath)
                                                                                                                                        .SetPassword(signingCertificatePassword)
                                                                                                                                        .SetFileDigestAlgorithm("sha256")
                                                                                                                                        .SetRfc3161TimestampServerUrl(timestampUrl)
                                                                                                                                        .SetProcessToolPath(signingTool)
                                                                                                                                        .SetDescription("Octopus Deploy Calamari")
                                                                                                                                        .SetUrl("https://octopus.com")
                                                                                                                                        .SetFiles(filesToSign.Select(x => x.ToString())));

                                                                                                     Log.Information("Finished signing {FileCount} files", filesToSign.Count);
                                                                                                 });
                      }, new RetryState(), CancellationToken.None);
    }

    /// <summary>
    /// Signs the files using the production certificate stored in Azure Key Vault.
    /// Relies on JSign, and requires a Docker container host to be available.
    /// </summary>
    /// <remarks>
    /// Access to the production Azure Key Vault is restricted.
    /// Use <see cref="SignWithDevelopmentSelfSignedCertOnWindows"/> for local development.
    /// </remarks>
    async Task SignWithProductionCertWithJSign(AbsolutePath parentDirectory, IReadOnlyCollection<AbsolutePath> filesToSign)
    {
        Log.Information("Signing {SignCount} files using JSign", filesToSign.Count);

        var nonConformingFiles = filesToSign
                                 .Where(x => !x.ToString()!.StartsWith(parentDirectory))
                                 .Distinct()
                                 .ToArray();

        if (nonConformingFiles.Any())
        {
            Log
                .ForContext(nameof(filesToSign), filesToSign)
                .Error("Because of container volume mapping restrictions " +
                       "all files to be signed should be inside the {ParentDirectory} or its subfolders", parentDirectory);

            throw new ArgumentException($"{nameof(filesToSign)} must all be in {parentDirectory} or its subfolders. " +
                                        "We are mapping a single directory in Docker. " +
                                        "Mapping files from different directories will not work with the current volume mapping approach.");
        }

        var azureTokenResponse = await GetAzureBearerToken(azureKeyVaultSettings);

        Logging.InBlock($"Code-signing and time-stamping files using JSign", () =>
                                                                             {
                                                                                 var jSignArgs = new List<string>
                                                                                 {
                                                                                     "--storetype", "AZUREKEYVAULT",
                                                                                     "--keystore", azureKeyVaultSettings.AzureKeyVaultUrl!,
                                                                                     "--alias", azureKeyVaultSettings.AzureKeyVaultCertificateName!,
                                                                                     "--storepass", azureTokenResponse.AccessToken,
                                                                                     "--tsaurl", string.Join(",", SigningTimestampUrls) // pass multiple so it can fall back to the next if one fails
                                                                                 };

                                                                                 const string volPath = "/usr/src/myapp";

                                                                                 var mappedFilesToSign = filesToSign
                                                                                     .Select(x => Path
                                                                                                  .Combine(volPath, Path.GetRelativePath(parentDirectory, x))
                                                                                                  .Replace("\\", "/") // We are replacing the slashes here to keep compatibility with Windows.
                                                                                     );

                                                                                 jSignArgs.AddRange(mappedFilesToSign);

                                                                                 Retry.Execute((state, ct) =>
                                                                                               {
                                                                                                   //we want retries here, as sometimes we have trouble pulling the container from artifactory
                                                                                                   var dockerRunSettings = new DockerContainerRunSettings()
                                                                                                                           .SetImage(jSignDockerImage)
                                                                                                                           .SetVolume(string.Join(":", parentDirectory, volPath))
                                                                                                                           .SetArgs(state.JSignArgs)
                                                                                                                           .SetProcessInvocationLogging(false);

                                                                                                   //manual logging to avoid logging the Azure token
                                                                                                   Log.Information("> {ToolPath} {Arguments}",
                                                                                                       dockerRunSettings.ProcessToolPath.DoubleQuoteIfNeeded(),
                                                                                                       string.Join(" ", dockerRunSettings.Args).Replace(state.AzureTokenResponse.AccessToken, "REDACTED"));

                                                                                                   DockerTasks.DockerContainerRun(dockerRunSettings);
                                                                                               }, new DockerRunRetryState(jSignArgs, azureTokenResponse), CancellationToken.None);
                                                                             });

        Log.Information("Finished signing {FileCount} files", filesToSign.Count);
    }

    async Task<AzureTokenResponse> GetAzureBearerToken(AzureKeyVaultSettings keyVaultSettings, string resource = "https://vault.azure.net")
    {
        using var client = new HttpClient();
        var url = $"https://login.microsoftonline.com/{keyVaultSettings.AzureKeyVaultTenantId}/oauth2/token";

        var valueDict = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", keyVaultSettings.AzureKeyVaultAppId! },
            { "client_secret", keyVaultSettings.AzureKeyVaultAppSecret! },
            { "resource", resource },
        };
        var content = new FormUrlEncodedContent(valueDict);

        var httpResponse = await client.PostAsync(url, content);
        httpResponse.EnsureSuccessStatusCode();

        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
        var tokenResponse = JsonConvert.DeserializeObject<AzureTokenResponse>(jsonResponse);
        if (tokenResponse?.AccessToken is null)
        {
            // The response should not be logged anywhere since it could contain sensitive data.
            throw new Exception("Error parsing Azure token response.");
        }

        return tokenResponse;
    }

    /// <summary>
    /// Signs files using the production certificate stored in Azure Key Vault.
    /// Relies on AzureSignTool, and will only work on Windows.
    /// </summary>
    /// <remarks>
    /// Access to the production Azure Key Vault is restricted.
    /// Use <see cref="SignWithDevelopmentSelfSignedCertOnWindows"/> for local development.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    void SignWithProductionCertWithAzureSignTool(IReadOnlyCollection<AbsolutePath> filesToSign,
                                                 string display = "",
                                                 string displayUrl = "")
    {
        Log.Information("Signing {SignCount} files using AzureSignTool", filesToSign.Count);

        Retry.Execute((state, ct) =>
                      {
                          //we want retries here because the timestamp servers can be flaky
                          var timestampUrl = GetTimestampUrl(state.AttemptNumber++);

                          Logging.InBlock($"Trying to sign and time stamp using {timestampUrl}", () =>
                                                                                                 {
                                                                                                     var settings = new AzureSignToolSettings()
                                                                                                                    .SetKeyVaultUrl(azureKeyVaultSettings.AzureKeyVaultUrl)
                                                                                                                    .SetKeyVaultTenantId(azureKeyVaultSettings.AzureKeyVaultTenantId)
                                                                                                                    .SetKeyVaultClientId(azureKeyVaultSettings.AzureKeyVaultAppId)
                                                                                                                    .SetKeyVaultClientSecret(azureKeyVaultSettings.AzureKeyVaultAppSecret)
                                                                                                                    .SetKeyVaultCertificateName(azureKeyVaultSettings.AzureKeyVaultCertificateName)
                                                                                                                    .SetDescription(display)
                                                                                                                    .SetDescriptionUrl(displayUrl)
                                                                                                                    .SetFileDigest(AzureSignToolDigestAlgorithm.sha256)
                                                                                                                    .SetTimestampRfc3161Url(timestampUrl)
                                                                                                                    .SetFiles(filesToSign.Select(x => x.ToString()));

                                                                                                     // In TeamCity the source code won't be available to read the _build.csproj PackageDownload reference.
                                                                                                     // In this case we use the AzureSignTool.dll coming from the nukebuild.zip artifact.
                                                                                                     var azureSignToolPath = KnownPaths.RootDirectory / "nukebuild" / "AzureSignTool" / "AzureSignTool.dll";
                                                                                                     if (azureSignToolPath.FileExists())
                                                                                                     {
                                                                                                         settings = settings.SetProcessToolPath(azureSignToolPath);
                                                                                                     }

                                                                                                     AzureSignToolTasks.AzureSignTool(settings);
                                                                                                 });
                      }, new RetryState(), CancellationToken.None);

        Log.Information("Finished signing {FileCount} files", filesToSign.Count);
    }

    /// <summary>
    ///  returns the next timestamp url to use, looping back to the start as many times as necessary
    /// </summary>
    static string GetTimestampUrl(int zeroBasedAttemptNumber)
        => SigningTimestampUrls[zeroBasedAttemptNumber % SigningTimestampUrls.Length];

    // note: Doesn't check if existing signatures are valid, only that one exists
    // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
    public static bool HasAuthenticodeSignature(string filePath)
    {
        try
        {
            // The suggested replacements don't check the AuthenticodeSignature
#pragma warning disable SYSLIB0057
            X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
            return true;
        }
        catch
        {
            return false;
        }
    }
}

class DockerRunRetryState(List<string> jSignArgs, AzureTokenResponse azureTokenResponse)
{
    public List<string> JSignArgs { get; init; } = jSignArgs;
    public AzureTokenResponse AzureTokenResponse { get; init; } = azureTokenResponse;
}