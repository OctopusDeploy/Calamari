using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target NetCoreTesting =>
        target => target
            .Executes(() =>
            {
                const string testFilter =
                    "TestCategory != Windows & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux";

                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile("Binaries/Calamari.Tests.dll")
                    .SetFilter(testFilter)
                    .SetProcessExitHandler(
                        process => process.ExitCode switch
                        {
                            0 => null, //successful
                            1 => null, //some tests failed
                            _ => throw new ProcessException(process)
                        }));
            });


    [PublicAPI]
    Target OncePerWindowsOrLinuxTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "(TestCategory != Windows & TestCategory != PlatformAgnostic) | TestCategory = RunOnceOnWindowsAndLinux";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               }));
                      });
    
    
    [PublicAPI]
    Target OncePerWindowsTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory != macOs & TestCategory != Nix & TestCategory != PlatformAgnostic & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ModifiesSystemProxy";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               }));
                      });
    
    [PublicAPI]
    Target WindowsSystemProxyTesting =>
        target => target
            .Executes(() =>
                      {
                          const string testFilter = "TestCategory = Windows & TestCategory = ModifiesSystemProxy";

                          DotNetTasks.DotNetTest(settings => settings
                                                             .SetProjectFile("Binaries/Calamari.Tests.dll")
                                                             .SetFilter(testFilter)
                                                             .SetProcessExitHandler(process => process.ExitCode switch
                                                                                               {
                                                                                                   0 => null, //successful
                                                                                                   1 => null, //some tests failed
                                                                                                   _ => throw new ProcessException(process)
                                                                                               }));
                      });
}