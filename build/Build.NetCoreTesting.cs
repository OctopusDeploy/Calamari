using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.CI.TeamCity;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target PlatformAgnosticTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory = PlatformAgnostic";
                          
                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });

    [PublicAPI]
    Target LinuxSpecificTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory != Windows & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });

    [PublicAPI]
    Target OncePerWindowsOrLinuxTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "(TestCategory != Windows & TestCategory != PlatformAgnostic) | TestCategory = RunOnceOnWindowsAndLinux";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });

    [PublicAPI]
    Target OncePerWindowsTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory != macOs & TestCategory != Nix & TestCategory != PlatformAgnostic & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ModifiesSystemProxy";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });

    [PublicAPI]
    Target WindowsSystemProxyTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory = Windows & TestCategory = ModifiesSystemProxy";

                          var runningInTeamCity = TeamCity.Instance is not null;
                          var outputDirectory = RootDirectory / "outputs";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               })
                                                             .SetTestAdapterPath(outputDirectory)
                                                             .AddLoggers("console;verbosity=normal")
                                                             .When(runningInTeamCity, x => x.EnableTeamCityTestLogger(outputDirectory)));
                      });
}