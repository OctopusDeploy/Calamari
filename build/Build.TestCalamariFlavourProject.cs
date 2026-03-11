using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [Parameter(Name = "ProjectToTest")] readonly string? CalamariProjectToTest;
    
    [PublicAPI]
    Target LinuxHostTests =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          var project = CalamariProjectToTest ?? "Calamari";

                          var dll = $"TestBinaries/{project}.Tests.dll";

                          CreateTestRun(dll)
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Windows") // Linux is all tests that are non-Windows specific
                              .Execute();
                      });
    
    [PublicAPI]
    Target WindowsHostTests =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          var project = CalamariProjectToTest ?? "Calamari";

                          var dll = $"TestBinaries/{project}.Tests.dll";

                          CreateTestRun(dll)
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Linux & TestCategory != MacOs & TestCategory != Windows2016OrLater") // Windows is all tests that are non-Linux or MacOS specific and not Win 2016 or later
                              .Execute();
                      });
    
    [PublicAPI]
    Target Windows2026OrLaterHostTests =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          var project = CalamariProjectToTest ?? "Calamari";

                          var dll = $"TestBinaries/{project}.Tests.dll";

                          CreateTestRun(dll)
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory = Windows2016OrLater")  // Windows 2016 or later specific tests
                              .Execute();
                      });
}