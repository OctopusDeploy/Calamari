using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target TestCalamariExternalTools =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          // Runs against a real external CLI tool (e.g. terraform), downloaded or found on PATH -
                          // not part of the default per-commit pipeline, invoked as its own nightly/on-demand build.
                          CreateTestRun("CalamariTests/Calamari.ExternalTools.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory=ExternalTool")
                              .Execute();
                      });
}
