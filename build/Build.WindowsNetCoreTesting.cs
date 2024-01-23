using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target WindowsNetCoreTesting =>
        target => target
            .Executes(() =>
            {
                const string testFilter =
                    "TestCategory != macOs & TestCategory != Nix & TestCategory != PlatformAgnostic & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux";

                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile("Binaries/Calamari.Tests.dll")
                    .SetFilter(testFilter)
                    .SetLoggers("trx")
                    .SetProcessExitHandler(
                        process => process.ExitCode switch
                        {
                            0 => null, //successful
                            1 => null, //some tests failed
                            _ => throw new ProcessException(process)
                        }));
            });
}