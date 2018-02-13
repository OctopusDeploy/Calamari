using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Integration.Packages.NuGet;
using Calamari.Util;
using Octopus.Versioning;
using Octopus.Versioning.Maven;
using Octopus.Versioning.Semver;

namespace Calamari.Integration.Packages
{
    public class PackagePhysicalFileMetadata : PackageFileNameMetadata
    {
        public string FullFilePath { get; }
        public string Hash { get; }
        public long Size { get; }

        public PackagePhysicalFileMetadata(PackageFileNameMetadata identity, string fullFilePath, string hash, long size) : base(identity.PackageId, identity.Version, identity.Extension)
        {
            FullFilePath = fullFilePath;
            Hash = hash;
            Size = size;
        }

        public static PackagePhysicalFileMetadata Build(string fullFilePath)
        {
            return Build(fullFilePath, PackageName.FromFile(fullFilePath));
        }

        public static PackagePhysicalFileMetadata Build(string fullFilePath, PackageFileNameMetadata identity)
        {
            if (identity == null)
                return null;
            try
            {
                using (var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read))
                {
                    return new PackagePhysicalFileMetadata(identity, fullFilePath, HashCalculator.Hash(stream), stream.Length);
                }
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    public class PackageFileNameMetadata
    {
        public string PackageId { get; }
        public IVersion Version { get; }
        public string Extension { get; }

        public PackageFileNameMetadata(string packageId, IVersion version, string extension)
        {
            PackageId = packageId;
            Version = version;
            Extension = extension;
        }
    }

    public static class PackageName
    {
        internal const char SectionDelimiter = '@';

        public static string ToCachedFileName(string packageId, IVersion version, string extension)
        {
            var cacheBuster = BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty);
            return SearchPattern(packageId, version, extension, cacheBuster);
        }

        static string SearchPattern(string packageId, IVersion version = null, string extension = null, string cacheBuster = null)
        {
            var ver = version == null ? "*": EncodeVersion(version);
            return $"{Encode(packageId)}{SectionDelimiter}{ver}{SectionDelimiter}{cacheBuster ?? "*"}{extension ?? "*"}";
        }

        internal static string ToRegexPattern(string packageId, IVersion version, string rootDir = "")
        {
            var pattern = SearchPattern(packageId, version, string.Empty, string.Empty);
            if (!string.IsNullOrWhiteSpace(rootDir))
            {
                pattern = Path.Combine(rootDir, pattern);
            }
            return Regex.Escape(pattern) + "[a-fA-F0-9]{32}\\..*\\b";
        }

        public static string[] ToSearchPatterns(string packageId, IVersion version = null, string[] extensions = null)
        {
            extensions = extensions ?? new[] {"*"};

            // Lets not bother tring to also match old pattern
            return extensions.Select(ext => $"{SearchPattern(packageId, version, ext)}")
                //.Union(extensions.Select(ext =>$"{Encode(packageId)}.{(version == null ? "*" : Encode(version.ToString()))}{ext}-*"))
                .Distinct().ToArray();
        }

        /// <summary>
        ///  Old school parser for those file not yet sourced directly from the cache.
        /// Expects pattern {PackageId}.{Version}.{Extension}
        /// Issue with extension parser if pre-release tag is present where two part extendsions are incorrectly split.
        /// e.g. MyApp.1.0.0-beta.tar.gz  => .tar forms part of pre-release tag and not extension
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="packageId"></param>
        /// <param name="version"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        static bool TryParseUnsafeFileName(string fileName, out string packageId, out IVersion version, out string extension)
        {
            packageId = null;
            version = null;
            extension = null;
            const string packageIdPattern = @"(?<packageId>(\w+([_.-]\w+)*?))";
            const string semanticVersionPattern = @"(?<semanticVersion>(\d+(\.\d+){0,3}" // Major Minor Patch
                                                  + @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)" // Pre-release identifiers
                                                  + @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)"; // Build Metadata
            const string extensionPattern = @"(?<extension>(\.([a-zA-Z0-9])+)+)"; //Extension (wont catch two part extensions like .tar.gz if there is a pre-release tag)

            var match = Regex.Match(fileName, $@"^{packageIdPattern}\.{semanticVersionPattern}{extensionPattern}$", RegexOptions.IgnoreCase);

            var packageIdMatch = match.Groups["packageId"];
            var versionMatch = match.Groups["semanticVersion"];
            var extensionMatch = match.Groups["extension"];

            if (!packageIdMatch.Success || !versionMatch.Success || !extensionMatch.Success)
                return false;

            if (!VersionFactory.TryCreateSemanticVersion(versionMatch.Value, out version, true))
                return false;

            packageId = packageIdMatch.Value;
            extension = extensionMatch.Value;

            return true;
        }

        static bool TryParseEncodedFileName(string fileName, out string packageId, out IVersion version,
            out string extension)
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

        public static PackageFileNameMetadata FromFile(string path)
        {
            var fileName = Path.GetFileName(path) ?? "";

            if (!TryParseEncodedFileName(fileName, out var packageId, out var version, out var extension) &&
                !TryParseUnsafeFileName(fileName, out packageId, out version, out extension)) {
                throw new Exception($"Unexpected file format in {fileName}.\nExpected Octopus cached file format: `<PackageId>{SectionDelimiter}<Version>{SectionDelimiter}<CacheBuster>.<Extension>` or `<PackageId>.<SemverVersion>.<Extension>`");
            }

            //TODO: Extract... Obviously _could_ be an issue for .net core
            if (extension.Equals(".nupkg", StringComparison.InvariantCultureIgnoreCase) && File.Exists(path))
            {
//                var metaData = new FileSystemNuGetPackage(path);
//                version = metaData.Version;
//                packageId = metaData.PackageId;
                var metaData = new LocalNuGetPackage(path).Metadata;
                version = SemVerFactory.CreateVersion(metaData.Version.ToString());
                packageId = metaData.Id;
            }

            return new PackageFileNameMetadata(packageId, version, extension);
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
                case MavenVersion mavenVersion:
                    return "M" + Encode(version.ToString());
                case Octopus.Versioning.Semver.SemanticVersion semver:
                    return "S" + Encode(version.ToString());
            }

            throw new Exception($"Unrecognised Version format `{typeof(Version).Name}`");
        }

        static IVersion DecodeVersion(string input)
        {
            switch (input[0])
            {
                case 'S':
                    return VersionFactory.CreateSemanticVersion(input.Substring(1), true);
                case 'M':
                    return VersionFactory.CreateMavenVersion(input.Substring(1));
            }
            throw new Exception($"Unrecognised Version format `{input}`");
        }
    }
}
