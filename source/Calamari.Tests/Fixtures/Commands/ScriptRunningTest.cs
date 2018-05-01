using Autofac;
using Calamari.Integration.Processes;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;

        private string[] Args =>
            ScriptRunningTest.FullLocalPath(typeof(ScriptRunningTest).Assembly)
                .Map(dllPath => Path.GetDirectoryName(dllPath))
                .Map(dllDir => Path.Combine(dllDir, "Scripts"))
                .Map(scriptPath => new[]
                    {"run-test-script", "--script=" + scriptPath + "\\awsscript.ps1", "--extensions=Aws,Azure,Tests"});

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
        }
    }
}
