using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Nuke.Common.Tools.AzureSignTool;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using Serilog;

namespace Calamari.Build
{
    public static class Signing
    {
        static readonly string[] TimestampUrls =
        {
            "http://timestamp.digicert.com?alg=sha256",
            "http://timestamp.comodoca.com"
        };

        public static void SignAndTimestampBinaries(
            string outputDirectory,
            string? azureKeyVaultUrl,
            string? azureKeyVaultAppId,
            string? azureKeyVaultAppSecret,
            string? azureKeyVaultCertificateName,
            string? signingCertificatePath,
            string? signingCertificatePassword)
        {
            Log.Information("Signing binaries in {OutputDirectory}", outputDirectory);

            // check that any unsigned libraries, that Octopus Deploy authors, get
            // signed to play nice with security scanning tools
            // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
            // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
            var unsignedExecutablesAndLibraries = GetFilesFromDirectory(outputDirectory,
                                                                        "Calamari*.exe",
                                                                        "Calamari*.dll",
                                                                        "Octo*.exe",
                                                                        "Octo*.dll")
                                                  .Where(f => !HasAuthenticodeSignature(f))
                                                  .ToArray();

            if (unsignedExecutablesAndLibraries.IsEmpty())
            {
                Log.Information("No unsigned binaries to sign in {OutputDirectory}", outputDirectory);
                return;
            }

            if (azureKeyVaultUrl.IsNullOrEmpty() &&
                azureKeyVaultAppId.IsNullOrEmpty() &&
                azureKeyVaultAppSecret.IsNullOrEmpty() &&
                azureKeyVaultCertificateName.IsNullOrEmpty())
            {
                if (signingCertificatePath.IsNullOrEmpty() ||
                    signingCertificatePassword.IsNullOrEmpty())
                    throw new InvalidOperationException("Either Azure Key Vault or Signing " +
                        "Certificate Parameters must be set");

                if (!OperatingSystem.IsWindows())
                    throw new InvalidOperationException("Non-windows builds must either leave binaries " +
                        "unsigned or sign using the AzureSignTool");

                Log.Information("Signing files using signtool and the self-signed development code signing certificate");
                SignFilesWithSignTool(
                    unsignedExecutablesAndLibraries,
                    signingCertificatePath!,
                    signingCertificatePassword!);
            }
            else
            {
                Log.Information("Signing files using azuresigntool and the production code signing certificate");
                SignFilesWithAzureSignTool(
                    unsignedExecutablesAndLibraries,
                    azureKeyVaultUrl!,
                    azureKeyVaultAppId!,
                    azureKeyVaultAppSecret!,
                    azureKeyVaultCertificateName!);
            }
        }

        static IEnumerable<string> GetFilesFromDirectory(string directory, params string[] searchPatterns) =>
            searchPatterns.SelectMany(searchPattern => Directory.GetFiles(directory, searchPattern));

        // note: Doesn't check if existing signatures are valid, only that one exists
        // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
        static bool HasAuthenticodeSignature(string filePath)
        {
            try
            {
                X509Certificate.CreateFromSignedFile(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void SignFilesWithAzureSignTool(
            ICollection<string> files,
            string vaultUrl,
            string vaultAppId,
            string vaultAppSecret,
            string vaultCertificateName,
            string display = "",
            string displayUrl = "")
        {
            Log.Information("Signing {FilesCount} files using Azure Sign Tool", files.Count);

            TrySignTaskWithEachTimestampUrlUntilSuccess(url =>
                AzureSignToolTasks.AzureSignTool(_ =>
                    _.SetKeyVaultUrl(vaultUrl)
                     .SetKeyVaultClientId(vaultAppId)
                     .SetKeyVaultClientSecret(vaultAppSecret)
                     .SetKeyVaultCertificateName(vaultCertificateName)
                     .SetFileDigest("sha256")
                     .SetDescription(display)
                     .SetDescriptionUrl(displayUrl)
                     .SetTimestampRfc3161Url(url)
                     .SetTimestampDigest(AzureSignToolDigestAlgorithm.sha256)
                     .SetFiles(files)));

            Log.Information("Finished signing {FilesCount} files", files.Count);
        }

        static void SignFilesWithSignTool(
            IReadOnlyCollection<string> files,
            string certificatePath,
            string certificatePassword,
            string display = "",
            string displayUrl = "")
        {
            if (!File.Exists(certificatePath))
                throw new Exception($"The code-signing certificate was not found at {certificatePath}");

            Log.Information("Signing {FilesCount} files using certificate at '{CertificatePath}'...",
                            files.Count,
                            certificatePath);

            TrySignTaskWithEachTimestampUrlUntilSuccess(url =>
                SignToolTasks.SignTool(_ =>
                   _.SetFileDigestAlgorithm(SignToolDigestAlgorithm.SHA256)
                    .SetFile(certificatePath)
                    .SetPassword(certificatePassword)
                    .SetDescription(display)
                    .SetUrl(displayUrl)
                    .SetRfc3161TimestampServerUrl(url)
                    .SetTimestampServerDigestAlgorithm(SignToolDigestAlgorithm.SHA256)
                    .AddFiles(files)));

            Log.Information("Finished signing {FilesCount} files", files.Count);
        }

        static void TrySignTaskWithEachTimestampUrlUntilSuccess(Action<string> signTask)
        {
            foreach (var url in TimestampUrls)
            {
                try
                {
                    signTask(url);
                    break;
                }
                catch (Exception ex)
                {
                    if (url == TimestampUrls.Last())
                        throw;

                    Log.Error(ex, "Failed to sign files using timestamp url {Url}. Trying the next timestamp url", url);
                }
            }
        }
    }
}