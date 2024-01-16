using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.Extensions;
using Octopus.Versioning;
using Octopus.Versioning.Lexicographic;
using Octopus.Versioning.Maven;
using Octopus.Versioning.Octopus;
using Octopus.Versioning.Semver;

/// These classes are shared with Octopus.Server. Ideally should be moved to a common location.
namespace Calamari.Common.Features.Packages
{
    public static class PackageName
    {
        internal const char SectionDelimiter = '@';

        public static string ToCachedFileName(string packageId, IVersion version, string extension)
        {
            var cacheBuster = BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty);
            return SearchPattern(packageId, version, extension, cacheBuster);
        }

        static string SearchPattern(string packageId, IVersion? version = null, string? extension = null, string? cacheBuster = null)
        {
            var ver = version == null ? "*" : EncodeVersion(version);
            return $"{Encode(packageId)}{SectionDelimiter}{ver}{SectionDelimiter}{cacheBuster ?? "*"}{extension ?? "*"}";
        }

        public static string ToRegexPattern(string packageId, IVersion version, string rootDir = "")
        {
            var pattern = SearchPattern(packageId, version, string.Empty, string.Empty);
            if (!string.IsNullOrWhiteSpace(rootDir))
                pattern = Path.Combine(rootDir, pattern);
            return Regex.Escape(pattern) + "[a-fA-F0-9]{32}\\..*\\b";
        }

        public static string[] ToSearchPatterns(string packageId, IVersion? version = null, string[]? extensions = null)
        {
            extensions = extensions ?? new[] { "*" };

            // Lets not bother tring to also match old pattern
            return extensions.Select(ext => $"{SearchPattern(packageId, version, ext)}")
                //.Union(extensions.Select(ext =>$"{Encode(packageId)}.{(version == null ? "*" : Encode(version.ToString()))}{ext}-*"))
                .Distinct()
                .ToArray();
        }
        
        /// <summary>
        /// The PackageId could be supplied as a path within a Feed, e.g a Helm OCI feed allows a feed pointing at the root, but packages can be stored with paths:
        /// e.g
        /// - package1.tgz
        /// - folder/package2.tgz - the package id for this package would be folder/package2
        /// </summary>
        public static string ExtractPackageNameFromPathedPackageId(string packageId) => packageId.Contains("/") ? packageId.Split(new[]{'/'}, StringSplitOptions.RemoveEmptyEntries).Last() : packageId;

        /// <summary>
        /// Old school parser for those file not yet sourced directly from the cache.
        /// Expects pattern {PackageId}.{Version}.{Extension}
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="packageId"></param>
        /// <param name="version"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        static bool TryParseUnsafeFileName(string fileName,
            [NotNullWhen(true)]out string? packageId,
            [NotNullWhen(true)]out IVersion? version,
            [NotNullWhen(true)]out string? extension)
        {
            packageId = null;
            version = null;
            extension = null;
            const string packageIdPattern = @"(?<packageId>(\w+([_.-]\w+)*?))";
            const string semanticVersionPattern = @"(?<semanticVersion>(\d+(\.\d+){0,3}" // Major Minor Patch
                +
                @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)" // Pre-release identifiers
                +
                @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)"; // Build Metadata

            var fileNamePattern = $@"{packageIdPattern}\.{semanticVersionPattern}";

            // here we're getting opinionated about how we split up packages that have both
            // pre-release identifiers and/or build metadata and also a two-part extension name.
            // for example "foo.1.0.0-release.tar.gz" could mean:
            // - release tag: "release.tar" and extension: "gz"
            // - release tag: "release" and extension: "tar.gz"
            // The thinking here is
            // - "tar.gz" and friends are extensions we know about
            // - this is a method specific to packages and archives
            // - cases like the above "tar" are unlikely to be part of a release tag or build metadata
            // so the below method strips out the extensions we know about before performing
            // the match on the filename. If we don't find any extensions we know about
            // we fall back to trying to pattern match one in the filename.
            if (TryMatchKnownExtensions(fileName, out var strippedFileName, out var extensionMatch))
            {
                fileName = strippedFileName;
            }
            else
            {
                const string extensionPattern = @"(?<extension>(\.([a-zA-Z0-9])+)+)"; //Extension (wont catch two part extensions like .tar.gz if there is a pre-release tag)
                fileNamePattern += extensionPattern;
            }

            var match = Regex.Match(fileName, $"^{fileNamePattern}$", RegexOptions.IgnoreCase);

            var packageIdMatch = match.Groups["packageId"];
            var versionMatch = match.Groups["semanticVersion"];
            extensionMatch = extensionMatch.Success ? extensionMatch : match.Groups["extension"];

            if (!packageIdMatch.Success || !versionMatch.Success || !extensionMatch.Success)
                return false;

            version = VersionFactory.TryCreateSemanticVersion(versionMatch.Value.SanitiseSemVerString(), true);
            if (version == null)
                return false;

            packageId = packageIdMatch.Value;
            extension = extensionMatch.Value;

            return true;
        }

        static bool TryMatchKnownExtensions(string fileName, out string strippedFileName, out Group extensionMatch)
        {
            // At the moment we only have one use case for this: files ending in ".tar.xyz" (see tests)
            // But if in the future we have more, we can modify this method to accomodate more cases.
            var knownExtensionPatterns = @"\.tar((\.[a-zA-Z0-9]+)?)";
            var match = new Regex($"(?<fileName>.*)(?<extension>{knownExtensionPatterns})$").Match(fileName);

            strippedFileName = match.Success ? match.Groups["fileName"].Value : fileName;
            extensionMatch = match.Groups["extension"];

            return match.Success;
        }

        static bool TryParseEncodedFileName(string fileName,
            [NotNullWhen(true)]out string? packageId,
            [NotNullWhen(true)]out IVersion? version,
            [NotNullWhen(true)]out string? extension)
        {
            packageId = null;
            version = null;
            extension = null;
            var sections = fileName.Split(SectionDelimiter);

            if (sections.Length != 3)
                return false;

            try
            {
                packageId = Decode(sections[0]);
                version = DecodeVersion(sections[1]);
                var extensionStart = sections[2].IndexOf('.');
                extension = sections[2].Substring(extensionStart);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static PackageFileNameMetadata FromFile(string? path)
        {
            var fileName = Path.GetFileName(path) ?? "";

            if (!TryParseEncodedFileName(fileName, out var packageId, out var fileVersion, out var extension) &&
                !TryParseUnsafeFileName(fileName, out packageId, out fileVersion, out extension))
                throw new Exception($"Unexpected file format in {fileName}.\nExpected Octopus cached file format: `<PackageId>{SectionDelimiter}<Version>{SectionDelimiter}<CacheBuster>.<Extension>` or `<PackageId>.<SemverVersion>.<Extension>`");

            var version = fileVersion;
            //TODO: Extract... Obviously _could_ be an issue for .net core
            if (extension.Equals(".nupkg", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
//                var metaData = new FileSystemNuGetPackage(path);
//                version = metaData.Version;
//                packageId = metaData.PackageId;
                var metaData = new LocalNuGetPackage(path!).Metadata;
                version = SemVerFactory.CreateVersion(metaData.Version.ToString().SanitiseSemVerString());
                packageId = metaData.Id;
            }

            return new PackageFileNameMetadata(packageId, version, fileVersion, extension);
        }

        public static bool TryMatchTarExtensions(string fileName, out string strippedFileName, out string extension)
        {
            // At the moment we only have one use case for this: files ending in ".tar.xyz"
            // As that is the only format of multiple part extensions we currently supported: https://octopus.com/docs/packaging-applications
            // But if in the future we have more, we can modify this method to accomodate more cases.
            var knownExtensionPatterns = @"\.tar((\.[a-zA-Z0-9]+)?)";
            var match = new Regex($"(?<fileName>.*)(?<extension>{knownExtensionPatterns})$").Match(fileName);

            strippedFileName = match.Success ? match.Groups["fileName"].Value : fileName;
            extension = match.Groups["extension"].Value;

            return match.Success;
        }

        public static bool TryFromFile(string? path, out PackageFileNameMetadata? metadata)
        {
            try
            {
                metadata = FromFile(path);
                return true;
            }
            catch
            {
                metadata = default;
                return false;
            }
        }

        static string Encode(string input)
        {
            return FileNameEscaper.Escape(input);
        }

        static string Decode(string input)
        {
            return FileNameEscaper.Unescape(input);
        }

        static string EncodeVersion(IVersion version)
        {
            switch (version)
            {
                case MavenVersion _:
                    return "M" + Encode(version.ToString());
                case SemanticVersion _:
                    return "S" + Encode(version.ToString());
                case OctopusVersion _:
                    return "O" + Encode(version.ToString());
                case LexicographicSortedVersion _:
                    return "L" + Encode(version.ToString());
            }

            throw new Exception($"Unrecognised Version format `{version.GetType().Name}`");
        }

        static IVersion DecodeVersion(string input)
        {
            switch (input[0])
            {
                case 'S':
                    return VersionFactory.CreateSemanticVersion(Decode(input.Substring(1)).SanitiseSemVerString(), true);
                case 'M':
                    return VersionFactory.CreateMavenVersion(Decode(input.Substring(1)));
                case 'O':
                    return VersionFactory.CreateOctopusVersion(Decode(input.Substring(1)));
                case 'L':
                    return VersionFactory.CreateLexicographicSortedVersion(Decode(input.Substring(1)));
            }

            throw new Exception($"Unrecognised Version format `{input}`");
        }
    }
}