using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target NetCoreTesting =>
        target => target
            .Executes(() =>
            {
                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile("Binaries/Calamari.Tests.dll")
                    .SetFilter("TestCategory != Windows & TestCategory != fsharp & TestCategory != scriptcs & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux")
                    .SetLoggers("trx"));
            });
}

