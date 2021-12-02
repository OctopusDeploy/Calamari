using System;
using Calamari.Common.Plumbing.Commands.Options;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery
{
    public class CommandLineVersionFormatDiscovery : ITryToDiscoverVersionFormat
    {
        public bool TryDiscoverVersionFormat(IManagePackageUse journal, IVariables variables, string[] commandLineArguments, out VersionFormat format, VersionFormat defaultFormat = VersionFormat.Semver)
        {
            var success = false;
            var parsedFormat = defaultFormat;
            var customOptions = new OptionSet();
            customOptions.Add("packageVersionFormat=",
                              $"",
                              v =>
                              {
                                  success = Enum.TryParse(v, out parsedFormat);
                              });

            customOptions.Parse(commandLineArguments);
            format = success ? parsedFormat : defaultFormat;
            return success;
        }
    }
}