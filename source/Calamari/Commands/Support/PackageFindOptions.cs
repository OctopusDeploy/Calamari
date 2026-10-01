using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands.Options;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public class PackageFindOptions : IPackageFindOptions
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string PackageHash { get; set; }
        public bool ExactMatchOnly { get; set; }
        public VersionFormat VersionFormat { get; set; } = VersionFormat.Semver;

        public static void ConfigureOptions(OptionSet options, IPackageFindOptions findOptions)
        {
            options.Add("packageId=", "Package ID to find", v => findOptions.PackageId = v);
            options.Add("packageVersion=", "Package version to find", v => findOptions.PackageVersion = v);
            options.Add("packageHash=", "Package hash to compare against", v => findOptions.PackageHash = v);
            options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out VersionFormat format))
                    {
                        throw new CommandException($"The provided version format `{format}` is not recognised.");
                    }

                    findOptions.VersionFormat = format;
                });
            options.Add("exactMatch=", "Only return exact matches", v => findOptions.ExactMatchOnly = bool.Parse(v));
        }
    }
}
