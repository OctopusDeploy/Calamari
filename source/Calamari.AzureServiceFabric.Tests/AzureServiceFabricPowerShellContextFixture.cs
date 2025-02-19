using System;
using System.Collections.Generic;
using System.Fabric.Query;
using System.IO;
using Calamari.AzureServiceFabric.Integration;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using Calamari.Tests.Helpers;
using Shouldly;

namespace Calamari.AzureServiceFabric.Tests
{
    [TestFixture]
    public class AzureServiceFabricPowerShellContextFixture
    {
        [Test]
        [TestCase("Endpoint", ScriptSyntax.PowerShell, true)]
        [TestCase("", ScriptSyntax.PowerShell, false)]
        [TestCase("Endpoint", ScriptSyntax.FSharp, false)]
        public void ShouldBeEnabled(string connectionEndpoint, ScriptSyntax syntax, bool expected)
        {
            var variables = new CalamariVariables
            {
                { SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint }
            };
            var target = new AzureServiceFabricPowerShellContext(variables, ConsoleLog.Instance);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);
        }

        [Test]
        public void MyTest()
        {
            var log = ConsoleLog.Instance;
            var variables = GetVariables();
            var scriptWrapper = CreateScriptWrapper(variables);

            var cliRunner = new CommandLineRunner(log, variables);
                
            using (var contextScriptFile = new TemporaryFile(CreateScriptFile()))
            {
                var result = scriptWrapper.ExecuteScript(new Script(contextScriptFile.FilePath), ScriptSyntax.PowerShell, cliRunner, new Dictionary<string, string>());
                result.HasErrors.ShouldBeFalse();
            }
        }

        string CreateScriptFile()
        {
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var testDir = TestEnvironment.GetTestPath("ServiceFabricFixtures", "TempScripts");
            var scriptFile = Path.Combine(testDir, "script.ps1");
            fileSystem.OverwriteFile(scriptFile, "Write-Host 'Hello, World!'");
            return scriptFile;
        }

        static CalamariVariables GetVariables()
        {
            return new CalamariVariables
            {
                { SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, "" },
                { SpecialVariables.Action.ServiceFabric.SecurityMode, AzureServiceFabricSecurityMode.SecureAzureAD.ToString() },
                { SpecialVariables.Action.ServiceFabric.ServerCertThumbprint, "" },
                { SpecialVariables.Action.ServiceFabric.ClientCertVariable, "" },
                { SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, "" },
                { SpecialVariables.Action.ServiceFabric.CertificateStoreName, "" },
                { SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername, "mark@octopus.com" },
                { SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword, "AadUserCredentialPassword" },
                { SpecialVariables.Action.ServiceFabric.AadClientCredentialSecret, "" },
                { SpecialVariables.Action.ServiceFabric.AadCredentialType, "UserCredential" },
                { SpecialVariables.Action.ServiceFabric.CertificateFindType, ""},
                
            };
        }

        const ScriptSyntax Syntax = ScriptSyntax.PowerShell;

        AzureServiceFabricPowerShellContext CreateScriptWrapper(IVariables variables) => new AzureServiceFabricPowerShellContext(variables, ConsoleLog.Instance)
        {
            NextWrapper = new StubScriptWrapper().Enable()
        };
    }
}