#if NETFX
using Autofac;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Calamari.Tests.Hooks;
using NUnit.Framework;
using System;
using System.IO;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;

        // The Azure extensions are not used in testing because the machines do not have the required
        // PowerShell modules. i.e. you get the error:
        // The term 'Get-AzureRmEnvironment' is not recognized as the name of a cmdlet
        // You can uncomment the line below for local testing though.
        //private string Extensions = "--extensions=Calamari.Aws,Calamari.Azure,Calamari.Tests"; 

        private string Extensions = "--extensions=Calamari.Aws,Calamari.Tests";

        private string Script = GetFixtureResouce("Scripts", "awsscript.ps1");

        private string[] Args => new[] {"run-test-script", "--script=" + Script, Extensions};

        private static string GetFixtureResouce(params string[] paths)
        {
            var type = typeof(ScriptRunningTest);
            return GetFixtureResouce(type, paths);
        }

        private static string GetFixtureResouce(Type type, params string[] paths)
        {
            var path = type.Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }

        private CalamariVariableDictionary BuildVariables(CalamariVariableDictionary variables)
        {
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
            container = Calamari.Program.BuildContainer(Args);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void RunScript()
        {
            Assert.IsTrue(File.Exists(Script), Script + " must exist as a file");

            BuildVariables(container.Resolve<CalamariVariableDictionary>());
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
            // TestModule should have been loadded because we are treating the 
            // Calamari.Test dll as an extension. This means ScriptHookMock and
            // EnvironmentVariableHook have been placed in the container, and because
            // it is enabled they must have been called.
            Assert.IsTrue(container.Resolve<ScriptHookMock>().WasCalled);
        }
    }
}
#endif