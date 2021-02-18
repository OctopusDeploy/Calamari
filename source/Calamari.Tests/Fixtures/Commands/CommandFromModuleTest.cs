#if NETFX
using Calamari.Tests.Helpers;
using NUnit.Framework;
using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class CommandFromModuleTest
    {
        // The Azure extensions are not used in testing because the machines do not have the required
        // PowerShell modules. i.e. you get the error:
        // The term 'Get-AzureRmEnvironment' is not recognized as the name of a cmdlet
        // You can uncomment the line below for local testing though.
        //private string Extensions = "--extensions=Calamari.Aws,Calamari.Azure,Calamari.Tests";

        private string Script = GetFixtureResource("Scripts", "awsscript.ps1");

        private static string GetFixtureResource(params string[] paths)
        {
            var type = typeof(CommandFromModuleTest);
            return GetFixtureResource(type, paths);
        }

        private static string GetFixtureResource(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }

        private CalamariVariables BuildVariables()
        {
            var variables = new CalamariVariables();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AwsAccount");
            variables.Set("Octopus.Action.Aws.Region", "us-east-1");
            variables.Set("AwsAccount.AccessKey", ExternalVariables.Get(ExternalVariable.AwsAcessKey));
            variables.Set("AwsAccount.SecretKey", ExternalVariables.Get(ExternalVariable.AwsSecretKey));
            variables.Set("Octopus.Action.Aws.AssumeRole", "False");
            variables.Set("Octopus.Action.Aws.AssumedRoleArn", "");
            variables.Set("Octopus.Action.Aws.AssumedRoleSession", "");
            variables.Set("Octopus.Account.AccountType", "AzureServicePrincipal");
            variables.Set("Octopus.Action.Azure.TenantId", "2a881dca-3230-4e01-abcb-a1fd235c0981");
            variables.Set("Octopus.Action.Azure.ClientId", ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
            variables.Set("Octopus.Action.Azure.Password", ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));
            variables.Set("Octopus.Action.Azure.SubscriptionId", "cf21dc34-73dc-4d7d-bd86-041884e0bc75");

            return variables;
        }

        [SetUp]
        public void SetUp()
        {
            ExternalVariables.LogMissingVariables();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void RunScript()
        {
            Assert.IsTrue(File.Exists(Script), Script + " must exist as a file");

            using (var temp = new TemporaryFile(Path.GetTempFileName()))
            {
                BuildVariables().Save(temp.FilePath);

                var args = new[]
                {
                    "run-test-script",
                    "--script=" + Script,
                    "--extensions=Calamari.Aws,Calamari.Tests",
                    "--variables=" + temp.FilePath
                };

                ScriptHookMock.WasCalled = false;
                var retCode = Program.Main(args);
                Assert.AreEqual(0, retCode);
                // TestModule should have been loaded because we are treating the
                // Calamari.Test dll as an extension. This means ScriptHookMock and
                // EnvironmentVariableHook have been placed in the container, and because
                // it is enabled they must have been called.
                Assert.IsTrue(ScriptHookMock.WasCalled);
            }
        }
    }
}
#endif