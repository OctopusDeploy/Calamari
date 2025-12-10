using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace Calamari.Build.Utilities
{
    /// <summary>
    /// A Global.json file typically looks like this:
    ///     {
    ///     "sdk": {
    ///         "version": "6.0.300",
    ///         "rollForward": "latestFeature"
    ///       }
    ///     }
    /// </summary>
    public record GlobalJsonContents(string Version, string? RollForward);

    public abstract record DotNetDownloadStrategy
    {
        public record LatestInChannel(string Channel) : DotNetDownloadStrategy;

        public record Exact(string Version) : DotNetDownloadStrategy;
    }

    /// <summary>
    /// Helper code for dealing with .NET global.json files
    /// </summary>
    public class GlobalJson
    {
        public static DotNetDownloadStrategy DetermineDownloadStrategy(string version, string? rollForwardBehavior)
        {
            // we never roll forward a prerelease version. This is simply because we haven't written
            // the code to deal with this appropriately. If you find yourself wanting to supply a prerelease
            // version here, please update it
            if (version.Contains('-')) return new DotNetDownloadStrategy.Exact(version);

            var components = version.Split(".");

            return rollForwardBehavior switch
            {
                "disable" => new DotNetDownloadStrategy.Exact(version), // this might result in a search for a runtime that doesn't exist; garbage-in/garbage out
                
                "latestFeature" when components.Length == 3 => new DotNetDownloadStrategy.LatestInChannel($"{components[0]}.{components[1]}"), // "8.0" is considered a valid channel in Microsoft's distribution system, so we ask for that

                null => components.Length switch
                {
                    2 => new DotNetDownloadStrategy.LatestInChannel(version),
                    3 => new DotNetDownloadStrategy.Exact(version),
                    _ => throw new ArgumentException($"Can't figure out download strategy for version {version}")
                },
                
                _ => throw new NotSupportedException($"Unsupported rollForwardBehavior {rollForwardBehavior}")
            };
        }

        public static GlobalJsonContents Parse( string filePath)
            => Parse(File.ReadAllBytes(filePath), filePath);

        public static GlobalJsonContents Parse(byte[] utf8Bytes, string filePath)
        {
            using var doc = JsonDocument.Parse(utf8Bytes, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new FormatException($"could not parse {filePath}; root was not object");
            if (!root.TryGetProperty("sdk", out var sdkElement) || sdkElement.ValueKind != JsonValueKind.Object) throw new FormatException($"could not parse {filePath}; no 'sdk' node");
            if (!sdkElement.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String) throw new FormatException($"could not parse {filePath}; no 'sdk/version' node");

            var version = versionElement.GetString() ?? "";
            if (sdkElement.TryGetProperty("rollForward", out var rollForwardElement) && rollForwardElement.ValueKind == JsonValueKind.String)
            {
                return new GlobalJsonContents(version, rollForwardElement.GetString());
            }

            return new GlobalJsonContents(version, null);
        }

        public static string? Find(string startingDirectory, ILogger logger)
        {
            var directory = startingDirectory;
            while (directory is { Length: > 0 })
            {
                logger.Verbose("Looking for global.json in {Directory}", directory);
                var candidate = Path.Combine(directory, "global.json");

                if (File.Exists(candidate))
                {
                    Log.Information("Found {FilePath}", candidate);
                    return candidate;
                }

                var parent = Path.GetDirectoryName(directory);

                if (parent == directory) break;
                directory = parent;
            }

            return null;
        }
    }
}