using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target TestCalamariExternalCloudIntegrations =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          // Run only the tests that hit real external cloud services (e.g. real Azure) - the
                          // credential-requiring smoke suite kept out of the main flavour run. Combine with any
                          // caller-supplied VSTest_TestCaseFilter rather than overwriting it (WithFilter is last-wins).
                          const string onlyExternalCloud = "TestCategory=ExternalCloudIntegration";
                          var filter = string.IsNullOrWhiteSpace(CalamariFlavourTestCaseFilter)
                              ? onlyExternalCloud
                              : $"({CalamariFlavourTestCaseFilter}) & {onlyExternalCloud}";

                          foreach (var flavour in GetCalamariFlavours())
                          {
                              CreateTestRun($"CalamariTests/{flavour}.Tests.dll")
                                  .WithDotNetPath(dotnetPath)
                                  .WithFilter(filter)
                                  .Execute();
                          }
                      });
}
