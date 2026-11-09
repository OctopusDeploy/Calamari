using System;
using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target PlatformAgnosticTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory = PlatformAgnostic & TestCategory != ExternalCloudIntegration")
                              .Execute();
                      });

    [PublicAPI]
    Target LinuxSpecificTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Windows & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ExternalCloudIntegration")
                              .Execute();
                      });

    // Temporary redirect so I can merge this PR without breaking Teamcity builds
    [PublicAPI]
    Target LinuxSpecificTestingWithoutOpenSsl3 =>
        target => target.DependsOn(LinuxSpecificTestingWithoutWithoutOpenSsl11OrOpenSsl3);

    [PublicAPI]
    Target LinuxSpecificTestingWithoutWithoutOpenSsl11OrOpenSsl3 =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Windows & TestCategory != PlatformAgnostic & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != RequiresOpenSsl1_1OrOpenSsl3 & TestCategory != ExternalCloudIntegration")
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
                              .WithFilter("((TestCategory != Windows & TestCategory != PlatformAgnostic) | TestCategory = RunOnceOnWindowsAndLinux) & TestCategory != ExternalCloudIntegration")
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
                              .WithFilter("TestCategory != macOs & TestCategory != Nix & TestCategory != PlatformAgnostic & TestCategory != nixMacOS & TestCategory != RunOnceOnWindowsAndLinux & TestCategory != ModifiesSystemProxy & TestCategory != ExternalCloudIntegration")
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