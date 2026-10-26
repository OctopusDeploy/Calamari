using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [Parameter(Name = "CalamariFlavour")] readonly string? CalamariFlavourToTest;
    [Parameter(Name = "VSTest_TestCaseFilter")] readonly string? CalamariFlavourTestCaseFilter;

    [PublicAPI]
    Target TestCalamariFlavourProject =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          // Exclude tests that hit real external cloud services (e.g. real Azure) - these run as a
                          // separate smoke suite, not as part of the main flavour test run. Combine with any
                          // caller-supplied VSTest_TestCaseFilter rather than overwriting it (WithFilter is last-wins).
                          const string excludeExternalCloud = "TestCategory!=ExternalCloudIntegration";
                          var filter = string.IsNullOrWhiteSpace(CalamariFlavourTestCaseFilter)
                              ? excludeExternalCloud
                              : $"({CalamariFlavourTestCaseFilter}) & {excludeExternalCloud}";

                          CreateTestRun($"CalamariTests/Calamari.{CalamariFlavourToTest}.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter(filter)
                              .Execute();
                      });
}