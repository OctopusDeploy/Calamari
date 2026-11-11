using System;
using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target LinuxSpecificTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Windows & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ExternalCloudIntegration")
                              .Execute();
                      });

    [PublicAPI]
    Target OncePerWindowsOrLinuxTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("(TestCategory != Windows | TestCategory = RunOnceOnWindowsAndLinux) & TestCategory != ExternalCloudIntegration")
                              .Execute();
                      });

    [PublicAPI]
    Target OncePerWindowsTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != macOs & TestCategory != Nix & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ModifiesSystemProxy & TestCategory != ExternalCloudIntegration")
                              .Execute();
                      });

    [PublicAPI]
    Target WindowsSystemProxyTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory = Windows & TestCategory = ModifiesSystemProxy")
                              .Execute();
                      });
}