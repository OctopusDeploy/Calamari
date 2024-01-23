using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target MultiplatformTesting =>
        target => target
            .Executes(() =>
            {
                const string testFilter = "TestCategory = RunOnceOnWindowsAndLinux";

                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile("/Users/scottmerchant/Documents/dev/Calamari/source/Calamari.Tests/bin/Debug/net6.0/Calamari.Tests.dll")
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