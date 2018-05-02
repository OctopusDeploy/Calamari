#if NETFX
using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Integration.Processes;
using Calamari.Tests.Hooks;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;

        //private string Extensions = "--extensions=Aws,Azure,Tests"; // Enabling Azure breaks tests on Linux machines, but can be used for local testing
        private string Extensions = "--extensions=Tests";
    
        private string[] Args =>
            ScriptRunningTest.FullLocalPath(typeof(ScriptRunningTest).Assembly)
                .Map(Path.GetDirectoryName)
                .Map(dllDir => Path.Combine(dllDir, "Scripts"))
                .Map(scriptPath => new[]
                    {"run-test-script", "--script=" + scriptPath + Path.DirectorySeparatorChar + "awsscript.ps1", Extensions});

        private static string FullLocalPath(Assembly assembly) =>
            Uri.UnescapeDataString(new UriBuilder(assembly.CodeBase).Path).Replace("/", "\\");

        private CalamariVariableDictionary BuildVariables(CalamariVariableDictionary variables)
        {
            variables.Set("Octopus.Action.AwsAccount.Variable", "AwsAccount");
            variables.Set("Octopus.Action.Aws.Region", "us-east-1");
            variables.Set("AwsAccount.AccessKey", Environment.GetEnvironmentVariable("AWS.E2E.AccessKeyId"));
            variables.Set("AwsAccount.SecretKey", Environment.GetEnvironmentVariable("AWS.E2E.SecretKeyId"));
            variables.Set("Octopus.Action.Aws.AssumeRole", "False");
            variables.Set("Octopus.Action.Aws.AssumedRoleArn", "");
            variables.Set("Octopus.Action.Aws.AssumedRoleSession", "");
            variables.Set("Octopus.Account.AccountType", "AzureServicePrincipal");
            variables.Set("Octopus.Action.Azure.TenantId", Environment.GetEnvironmentVariable("Azure.E2E.TenantId"));
            variables.Set("Octopus.Action.Azure.ClientId", Environment.GetEnvironmentVariable("Azure.E2E.ClientId"));
            variables.Set("Octopus.Action.Azure.Password", Environment.GetEnvironmentVariable("Azure.E2E.Password"));
            variables.Set("Octopus.Action.Azure.SubscriptionId", Environment.GetEnvironmentVariable("Azure.E2E.SubscriptionId"));

            return variables;
        }

        [SetUp]
        public void SetUp()
        {
            EnvironmentVariables.EnsureVariablesExist();
            container = Calamari.Program.BuildContainer(Args);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        public void RunScript()
        {
            BuildVariables(container.Resolve<CalamariVariableDictionary>());
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
            // TestModule should have been loadded because we are treating the 
            // Calamari.Test dll as an extension. This means ScriptHookMock and
            // EnvironmentVariableHook have been placed in the container, and because
            // it is enabled they must have been called.
            Assert.IsTrue(container.Resolve<ScriptHookMock>().WasCalled);
            Assert.IsTrue(container.Resolve<EnvironmentVariableHook>().WasCalled);
        }
    }
}
#endif