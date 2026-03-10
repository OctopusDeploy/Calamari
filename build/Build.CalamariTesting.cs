using System;
using JetBrains.Annotations;

namespace Calamari.Build;

partial class Build
{
    [PublicAPI]
    Target CalamariLinuxTests =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Windows")
                              .Execute();
                      });
    
    

    [PublicAPI]
    Target CalamariWindowsTesting =>
        target => target
            .Executes(async () =>
                      {
                          var dotnetPath = await LocateOrInstallDotNetSdk();

                          CreateTestRun("Binaries/Calamari.Tests.dll")
                              .WithDotNetPath(dotnetPath)
                              .WithFilter("TestCategory != Linux && TestCategory != MacOs")
                              .Execute();
                      });
}