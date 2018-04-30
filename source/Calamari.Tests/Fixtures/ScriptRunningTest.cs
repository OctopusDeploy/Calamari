using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Integration.Processes;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    public class ScriptRunningTest
    {
        private IContainer container;
        private ILifetimeScope unitOfWorkScope;

        private string[] Args =>
            ScriptRunningTest.FullLocalPath(typeof(ScriptRunningTest).Assembly)
                .Map(dllPath => Path.GetDirectoryName(dllPath))
                .Map(dllDir => Path.Combine(dllDir, "Scripts"))
                .Map(scriptPath => new[]
                    {"run-test-script", "--script=" + scriptPath + "\\awsscript.ps1", "--extensions=Aws,Tests"});

        private static string FullLocalPath(Assembly assembly) =>
            Uri.UnescapeDataString(new UriBuilder(assembly.CodeBase).Path).Replace("/", "\\");

        private CalamariVariableDictionary BuildVariables()
        {
            var variables = new CalamariVariableDictionary();
            variables.Set("Octopus.Action.AwsAccount.Variable", "AwsAccount");
            variables.Set("Octopus.Action.Aws.Region", "us-east-1");
            variables.Set("AwsAccount.AccessKey", "AKIAIHVPNPPZOLA4TYVQ");
            variables.Set("AwsAccount.SecretKey", "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");
            variables.Set("Octopus.Action.Aws.AssumeRole", "False");
            variables.Set("Octopus.Action.Aws.AssumedRoleArn", "");
            variables.Set("Octopus.Action.Aws.AssumedRoleSession", "");
            return variables;
        }

        [SetUp]
        public void SetUp()
        {
            container = Calamari.Program.BuildContainer(Args);
            unitOfWorkScope = container.BeginLifetimeScope(builder =>
            {
                builder.RegisterInstance(BuildVariables()).AsSelf();
            });
        }

        [TearDown]
        public void TearDown()
        {
            unitOfWorkScope?.Dispose();
            container?.Dispose();
        }

        [Test]
        public void RunScript()
        {
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
        }
    }
}
