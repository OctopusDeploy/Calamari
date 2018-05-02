using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Calamari.Tests.Hooks;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;

        private string Extensions = "--extensions=Aws,Azure,Tests";

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
            Assert.IsTrue(File.Exists(Script), Script + " must exist as a file");

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