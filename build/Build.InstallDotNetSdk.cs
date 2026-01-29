using System;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Calamari.Build.Utilities;
using JetBrains.Annotations;
using Nuke.Common.Tooling;

namespace Calamari.Build;

public partial class Build
{
    [Parameter("Specify this if you want to install a particular version of the SDK. Otherwise the InstallDotNetSdk Target will use global.json to determine the .NET SDK Version", Name = "dotnet-version")]
    public static string? dotNetVersionParameter;

    /// <summary>
    /// This target only exists so you can run nuke InstallDotNetSdk outside of another Target.
    /// If you have some Target that wants to install the .NET SDK, please call the InstallDotNetSdkIfRequired()
    /// method directly.
    /// </summary>
    [PublicAPI]
    public Target InstallDotNetSdk => t => t.Executes(async () => await LocateOrInstallDotNetSdk(dotNetVersionParameter));

    /// <summary>
    /// Searches for an appropriate dotnet SDK and returns the path to `dotnet.exe` (or unix equivalent).
    /// Will install the SDK if it is not found.
    /// Note: If an SDK is installed, it will typically go into a temporary folder and not the system-wide one.
    /// This is why it's important to use returned path. If you call this method and later just shell out to "dotnet"
    /// you may get the wrong thing.
    /// </summary>
    /// <remarks>
    /// This implements the "rollForward" feature that is typically specified in a global.json file.
    /// If `specificVersion` is not supplied, and we resolve a global.json that says something like
    ///   { "version": "6.0.403", "rollForward": "latestFeature" }
    /// then it will go search the internet for the latest 6.0.x release and install that instead.
    ///
    /// If `specificVersion` is supplied and is a 2-part number, this invokes the rollForward behaviour as well,
    /// e.g. "6.0" will install the latest 6.0.x release. 
    /// </remarks>
    /// <param name="specificVersion">
    /// If set, will find or install a particular version of the .NET SDK e.g. 6.0.417.
    /// If not set, will look for an appropriate version by scanning for a global.json file
    /// </param>
    async Task<AbsolutePath> LocateOrInstallDotNetSdk(string? specificVersion = null)
    {
        var httpClient = new Lazy<HttpClient>(() => new HttpClient(), isThreadSafe: true);

        DotNetDownloadStrategy strategy;
        if (specificVersion != null)
        {
            Log.Information("Request to install .NET SDK using command-line parameter: {DotNetVersion}", specificVersion);
            strategy = GlobalJson.DetermineDownloadStrategy(specificVersion, null);
        }
        else
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(assemblyLocation) ?? throw new InvalidOperationException("can't determine directory for executing assembly");

            var globalJsonFile = GlobalJson.Find(directory, Log.Logger);
            if (globalJsonFile == null) throw new Exception("--dotnet-version parameter was not supplied, and could not find a global.json file to tell us the SDK version to install; aborting");

            var parsed = GlobalJson.Parse(globalJsonFile);
            Log.Information("Request to install .NET SDK using {GlobalJsonPath} with Version {Version} and RollForward {RollForward}",
                            globalJsonFile, parsed.Version, parsed.RollForward);

            strategy = GlobalJson.DetermineDownloadStrategy(parsed.Version, parsed.RollForward);
        }

        string targetSdkVersion;
        switch (strategy)
        {
            case DotNetDownloadStrategy.Exact exact:
                targetSdkVersion = exact.Version;
                break;

            case DotNetDownloadStrategy.LatestInChannel latest:
                targetSdkVersion = await DetermineLatestVersion(httpClient, latest.Channel);
                Log.Information("Using LatestInChannel strategy; found target version {Version} in channel {Channel}", targetSdkVersion, latest.Channel);
                break;

            default:
                throw new NotSupportedException($"Unhandled download strategy {strategy}");
        }

        if (DotNetSdkIsInstalled(targetSdkVersion))
        {
            // assume if it already exists we don't need to chmod
            Log.Information(".NET {DotNetVersion} is already installed", targetSdkVersion);
            return DotNetTasks.DotNetPath; // DotNetTasks.DotNetPath finds the system-default dotnet in program files or equivalent
        }

        var temporaryDotNetDirectory = TemporaryDirectory / $"dotnet-{targetSdkVersion}";
        if (Directory.Exists(temporaryDotNetDirectory))
        {
            Log.Information(".NET {DotNetVersion} is not known to `dotnet --list-sdks` but {temporaryDotNetDirectory} exists, assuming it is there",
                            targetSdkVersion, temporaryDotNetDirectory);

            // as above, assume if it already exists we don't need to chmod
            return temporaryDotNetDirectory / (OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        }

        Log.Information("{DotNetVersion} is not installed. Downloading the .NET sdk zip file", targetSdkVersion);

        var platform = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "linux"; // there are distro-specific packages e.g. debian, they aren't used anymore

        var temporaryArchivePath = await DownloadDotNetSdk(httpClient, targetSdkVersion, platform, ResolveDotNetArchitectureString(RuntimeInformation.OSArchitecture));
        try
        {
            Log.Information("Extracting {DotNetVersion} into {temporaryDotNetDirectory}", targetSdkVersion, temporaryDotNetDirectory);
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(temporaryDotNetDirectory);
                ZipFile.ExtractToDirectory(temporaryArchivePath, temporaryDotNetDirectory, overwriteFiles: true);
                return temporaryDotNetDirectory / "dotnet.exe";
            }
            else
            {
                Directory.CreateDirectory(temporaryDotNetDirectory);
                await using (var gzipStream = new GZipStream(File.OpenRead(temporaryArchivePath), CompressionMode.Decompress))
                {
                    await TarFile.ExtractToDirectoryAsync(gzipStream, temporaryDotNetDirectory, overwriteFiles: true);
                }

                var executablePath = temporaryDotNetDirectory / "dotnet";
                // On unix we need to chmod +x the executable so later tasks can run it
                executablePath.SetExecutable();
                return executablePath;
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryArchivePath);
            }
            catch
            {
                // Deliberate empty catch-block; we can't do much if we can't delete the temp file. Not a big deal
            }
        }
    }

    static bool DotNetSdkIsInstalled(string version)
    {
        // Format:
        // 6.0.414 [/usr/local/share/dotnet/sdk]
        var versions = DotNetTasks.DotNet("--list-sdks").Select(o => o.Text.Split(" ").First());
        return versions.Contains(version);
    }

    // possible values for architecture are [amd64, x64, x86, arm64, arm]
    // refer to Get-Machine-Architecture in build.ps1
    static string ResolveDotNetArchitectureString(Architecture architecture) => architecture switch
                                                                                {
                                                                                    Architecture.Arm or Architecture.Armv6 => "arm",
                                                                                    Architecture.Arm64 or Architecture.LoongArch64 => "arm64",
                                                                                    Architecture.X86 => "x86",
                                                                                    Architecture.X64 => "x64",
                                                                                    // explicitly reference known architectures so the compiler can tell us about new unknown ones when they are added.
                                                                                    Architecture.Wasm or Architecture.S390x => throw new NotSupportedException($"Unsupported OS architecture {architecture}"),
                                                                                    _ => throw new NotSupportedException($"Unknown OS architecture {architecture}"),
                                                                                };

    static async Task<string> DetermineLatestVersion(Lazy<HttpClient> httpClient, string requestedFuzzyVersion)
    {
        return await PerformOperationWithFeedAndRetries(async feed =>
                                                        {
                                                            var downloadUrl = $"{feed}/Sdk/{requestedFuzzyVersion}/latest.version";

                                                            Log.Information($"Attempting download of {downloadUrl}");
                                                            var response = await httpClient.Value.GetAsync(downloadUrl);
                                                            response.EnsureSuccessStatusCode();

                                                            var versionString = await response.Content.ReadAsStringAsync();

                                                            // sanity check, we should get an exact version number such as 6.0.417
                                                            if (versionString.Count(c => c == '.') != 2) throw new Exception($"Unexpected response {versionString} from {downloadUrl}, expecting a version number such as 8.0.100");

                                                            return versionString.Trim();
                                                        });
    }

    // This function is ported from https://dot.net/v1/dotnet-install.ps1 (and .sh variant for unix)
    // You're supposed to fetch and execute the script so Microsoft can keep it up to date,
    // but powershell downloading and executing scripts is slow and painful, particularly on older windows like 2016 where TLS1.2 isn't enabled.
    // So we rather just do it ourselves.
    // NOTE: Whenever we do a major .NET migration we should review Microsoft's dotnet-install.ps1 script
    // and update our code if they've changed any of the download links/etc. Last checked on the release of .NET 8
    //
    // returns a path to a temporary file containing the zip or tar.gz that has been downloaded
    static async Task<string> DownloadDotNetSdk(Lazy<HttpClient> httpClient, string requestedVersion, string platform, string architecture)
    {
        return await PerformOperationWithFeedAndRetries(async feed =>
                                                        {
                                                            var fileExtension = platform == "win" ? "zip" : "tar.gz";
                                                            // Note: Version must be an exact specific version like 6.0.401.

                                                            // refer Get-Download-Link in dotnet-install.ps1
                                                            // Note this URL works for full releases of .NET but isn't quite right for release candidates; the two copies of
                                                            // `requestedVersion` differ when fetching an RC. Next time we want to download an RC SDK we'll need to fix this
                                                            var downloadUrl = $"{feed}/Sdk/{requestedVersion}/dotnet-sdk-{requestedVersion}-{platform}-{architecture}.{fileExtension}";

                                                            Log.Information($"Attempting download of {downloadUrl}");
                                                            var targetFile = Path.GetTempFileName();
                                                            await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.ReadWrite);

                                                            var response = await httpClient.Value.GetAsync(downloadUrl);
                                                            response.EnsureSuccessStatusCode();

                                                            await response.Content.CopyToAsync(fileStream);

                                                            return targetFile;
                                                        });
    }

    // feeds are tried in this order
    static readonly string[] Feeds =
    {
        // CDN's
        "https://builds.dotnet.microsoft.com/dotnet",
        "https://ci.dot.net/public",

        // direct
        "https://dotnetcli.blob.core.windows.net/dotnet",
        "https://dotnetbuilds.blob.core.windows.net/public"
    };

    static async Task<T> PerformOperationWithFeedAndRetries<T>(Func<string, Task<T>> performOperation)
    {
        ExceptionDispatchInfo? lastException = null;
        foreach (var feed in Feeds.Concat(Feeds)) // get a retry on each feed with sneaky concat
        {
            try
            {
                return await performOperation(feed);
            }
            catch (Exception ex)
            {
                lastException = ExceptionDispatchInfo.Capture(ex);
                Log.Warning(ex, $"Exception occurred using feed {feed}");
                // carry on, let the foreach loop roll over to the next mirror
            }
        }

        lastException?.Throw();
        throw new Exception("PerformOperationWithFeedAndRetries did not return a result, but caught no exception? Are there any feeds?"); // shouldn't happen; last resort
    }
}