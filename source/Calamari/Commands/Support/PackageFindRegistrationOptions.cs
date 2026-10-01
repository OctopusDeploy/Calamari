using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands.Options;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public class PackageFindRegistrationOptions : IPackageFindOptions
    {
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string PackageHash { get; set; }
        public bool ExactMatchOnly { get; set; }
        public VersionFormat VersionFormat { get; set; } = VersionFormat.Semver;
        public string TaskId { get; private set; }

        public static void ConfigureOptions(OptionSet options, PackageFindRegistrationOptions findOptions)
        {
            PackageFindOptions.ConfigureOptions(options, findOptions);
            options.Add("taskId=", "No task ID was specified.", v => findOptions.TaskId = v);
        }
    }
}
