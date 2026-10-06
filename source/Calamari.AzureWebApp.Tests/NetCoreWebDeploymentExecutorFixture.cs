using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.AzureWebApp.Tests;

[TestFixture]
public class NetCoreWebDeploymentExecutorFixture
{
    [Test]
    [Category("PlatformAgnostic")]
    public void FailureMessageContainsTheErrorsTheShimReportedWithoutTheirLevel()
    {
        var errorOutput = "ERR|(6/10/2026 10:59:25 AM) An error occurred when the request was processed on the remote computer." + Environment.NewLine
                          + "ERR|An error was encountered when processing operation 'Delete File' on 'index.html'. ---> System.IO.IOException: Invalid access to memory location." + Environment.NewLine;

        var message = NetCoreWebDeploymentExecutor.FailureMessage(1, errorOutput);

        Assert.That(message,
                    Is.EqualTo("(6/10/2026 10:59:25 AM) An error occurred when the request was processed on the remote computer."
                               + Environment.NewLine
                               + "An error was encountered when processing operation 'Delete File' on 'index.html'. ---> System.IO.IOException: Invalid access to memory location."));
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void FailureMessageDoesNotRepeatAnError()
    {
        var errorOutput = "ERR|An error occurred when the request was processed on the remote computer." + Environment.NewLine
                          + "ERR|An error occurred when the request was processed on the remote computer." + Environment.NewLine;

        Assert.That(NetCoreWebDeploymentExecutor.FailureMessage(1, errorOutput),
                    Is.EqualTo("An error occurred when the request was processed on the remote computer."));
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void FailureMessageKeepsStdErrThatWasNotWrittenByTheShimLogger()
    {
        var errorOutput = "Unhandled Exception: System.IO.FileNotFoundException: Could not load file or assembly 'Microsoft.Web.Deployment'" + Environment.NewLine;

        Assert.That(NetCoreWebDeploymentExecutor.FailureMessage(1, errorOutput),
                    Is.EqualTo("Unhandled Exception: System.IO.FileNotFoundException: Could not load file or assembly 'Microsoft.Web.Deployment'"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("\r\n")]
    [Category("PlatformAgnostic")]
    public void FailureMessageIsNotEmptyWhenTheShimReportedNoErrors(string errorOutput)
    {
        Assert.That(NetCoreWebDeploymentExecutor.FailureMessage(1, errorOutput),
                    Does.StartWith("Calamari.AzureWebApp.NetCoreShim.exe exited with code 1"));
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void LogsEachLevelAtTheMatchingLevel()
    {
        var log = new InMemoryLog();
        var executor = new NetCoreWebDeploymentExecutor(log, CalamariPhysicalFileSystem.GetPhysicalFileSystem());

        executor.LogOutputMessage("VRB|verbose");
        executor.LogOutputMessage("DBG|debug");
        executor.LogOutputMessage("INF|info");
        executor.LogOutputMessage("WRN|warning");
        executor.LogOutputMessage("ERR|error");
        executor.LogOutputMessage("FTL|fatal");
        executor.LogOutputMessage("INF|RESULT|{}");

        Assert.That(log.MessagesVerboseFormatted, Is.EqualTo(new[] { "verbose", "debug" }));
        Assert.That(log.MessagesInfoFormatted, Is.EqualTo(new[] { "info" }));
        Assert.That(log.MessagesWarnFormatted, Is.EqualTo(new[] { "warning" }));
        Assert.That(log.MessagesErrorFormatted, Is.EqualTo(new[] { "error", "fatal" }));
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void LinesWithoutAKnownLevelAreLoggedInsteadOfDropped()
    {
        var log = new InMemoryLog();
        var executor = new NetCoreWebDeploymentExecutor(log, CalamariPhysicalFileSystem.GetPhysicalFileSystem());

        executor.LogOutputMessage("   at Microsoft.Web.Deployment.DeploymentAgent.HandleSync()");
        executor.LogOutputMessage("XYZ|unknown level");

        Assert.That(log.MessagesVerboseFormatted, Is.EqualTo(new[] { "   at Microsoft.Web.Deployment.DeploymentAgent.HandleSync()", "XYZ|unknown level" }));
    }
}
