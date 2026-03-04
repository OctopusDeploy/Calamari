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

                          CreateTestRun($"CalamariTests/Calamari.{CalamariFlavourToTest}.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter(CalamariFlavourTestCaseFilter)
                              .Execute();
                      });
}