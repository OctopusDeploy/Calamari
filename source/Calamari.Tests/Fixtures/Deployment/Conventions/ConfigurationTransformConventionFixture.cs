using System;
using System.Collections;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class ConfigurationTransformConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IConfigurationTransformer configurationTransformer;
        ITransformFileLocator transformFileLocator;
        RunningDeployment deployment;
        IVariables variables;
        InMemoryLog logs;

        [SetUp]
        public void SetUp()
        {
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            configurationTransformer = Substitute.For<IConfigurationTransformer>();
            logs = new InMemoryLog();
            transformFileLocator = new TransformFileLocator(fileSystem, logs);

            var deployDirectory = BuildConfigPath(null);

            variables = new CalamariVariables();
            variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.ConfigurationTransforms);
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, deployDirectory);

            deployment = new RunningDeployment(deployDirectory, variables);
        }

        void AddConfigurationVariablesFlag()
        {
            variables.Set(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
        }

        [Test]
        public void ShouldApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagIsSet()
        {
            AddConfigurationVariablesFlag();

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("bar.config", "bar.Release.config");
        }

        [Test]
        public void ShouldNotApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagNotSet()
        {
            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformNotRun("bar.config", "bar.Release.config");
        }

        [Test]
        public void ShouldApplyEnvironmentTransform()
        {
            const string environment = "Production";

            AddConfigurationVariablesFlag();
            variables.Set(DeploymentEnvironment.Name, environment);

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("bar.config", "bar.Release.config");
            AssertTransformRun("bar.config", "bar.Production.config");
        }

        [Test]
        public void ShouldApplyTenantTransform()
        {
            const string environment = "Production";
            const string tenant = "Tenant-1";

            AddConfigurationVariablesFlag();
            variables.Set(DeploymentEnvironment.Name, environment);
            variables.Set(DeploymentVariables.Tenant.Name, tenant);

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("bar.config", "bar.Release.config");
            AssertTransformRun("bar.config", "bar.Production.config");
            AssertTransformRun("bar.config", "bar.Tenant-1.config");
        }

        [Test]
        public void ShouldApplyNamingConventTransformsInTheRightOrder()
        {
            const string environment = "Production";
            const string tenant = "Tenant-1";

            AddConfigurationVariablesFlag();
            variables.Set(DeploymentEnvironment.Name, environment);
            variables.Set(DeploymentVariables.Tenant.Name, tenant);

            CreateConvention(deployment.Variables).Install(deployment);

            Received.InOrder(() =>
            {
                configurationTransformer.Received().PerformTransform(
                    Arg.Any<string>(),
                    Arg.Is<string>(s => s.Equals(BuildConfigPath("bar.Release.config"), StringComparison.OrdinalIgnoreCase)),
                    Arg.Any<string>());

                configurationTransformer.Received().PerformTransform(
                    Arg.Any<string>(),
                    Arg.Is<string>(s => s.Equals(BuildConfigPath("bar.Production.config"), StringComparison.OrdinalIgnoreCase)),
                    Arg.Any<string>());

                configurationTransformer.Received().PerformTransform(
                    Arg.Any<string>(),
                    Arg.Is<string>(s => s.Equals(BuildConfigPath("bar.Tenant-1.config"), StringComparison.OrdinalIgnoreCase)),
                    Arg.Any<string>());
            });
        }

        [Test]
        public void ShouldApplySpecificCustomTransform()
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "foo.bar.config => foo.config");

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("foo.config", "foo.bar.config");
        }

        [Test]
        [TestCase("foo.missing.config => foo.config")]
        [TestCase("config\\fizz.buzz.config => config\\fizz.config")]
        public void ShouldLogErrorIfUnableToFindFile(string transform)
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transform);

            CreateConvention(deployment.Variables).Install(deployment);
            logs.StandardOut.Should().Contain($"The transform pattern \"{transform}\" was not performed as no matching files could be found.");
        }

        [Test]
        [TestCase("foo.bar.config => foo.config", "foo.bar.config => foo.config")]
        [TestCase("*.bar.config => foo.config", "foo.bar.config => foo.config")]
        [TestCase("foo.bar.config => foo.config", "*.bar.config => foo.config")]
        public void ShouldLogErrorIfDuplicateTransform(string transformA, string transformB)
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformA + Environment.NewLine + transformB);

            CreateConvention(deployment.Variables).Install(deployment);
            logs.StandardOut.Should().Contain($"The transform pattern \"{transformB}\" was not performed as it overlapped with another transform.");
        }

        [Test]
        [TestCaseSource(nameof(AdvancedTransformTestCases))]
        public void ShouldApplyAdvancedTransformations(string sourceFile, string transformDefinition, string expectedAppliedTransform)
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformDefinition.Replace('\\', Path.DirectorySeparatorChar));

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun(sourceFile, expectedAppliedTransform);
            configurationTransformer.ReceivedWithAnyArgs(1).PerformTransform("", "", ""); // Only Called Once
        }

        [Test]
        public void ShouldApplyMultipleWildcardsToSourceFile()
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "*.bar.blah => bar.blah");

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("bar.blah", "foo.bar.blah");
            AssertTransformRun("bar.blah", "xyz.bar.blah");
            configurationTransformer.ReceivedWithAnyArgs(2).PerformTransform("", "", "");
        }

        [Test]
        public void ShouldApplyTransformToMulipleTargetFiles()
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "bar.blah => *.bar.blah");

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun("foo.bar.blah", "bar.blah");
            AssertTransformRun("xyz.bar.blah", "bar.blah");
            configurationTransformer.ReceivedWithAnyArgs(2).PerformTransform("", "", "");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        [TestCase("bar.blah => *.Bar.Blah", "xyz.bar.blah", "bar.blah")]
        [TestCase("*.Bar.Blah => bar.blah", "bar.blah", "xyz.bar.blah")]
        [TestCase("foo.bar.config => Foo.Config", "foo.config", "foo.bar.config")]
        public void CaseInsensitiveOnWindows(string pattern, string from, string to)
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, pattern);

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformRun(from, to);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
        [TestCase("bar.blah => *.Bar.Blah", "xyz.bar.blah", "bar.blah")]
        [TestCase("*.Bar.Blah => bar.blah", "bar.blah", "xyz.bar.blah")]
        [TestCase("foo.bar.config => Foo.Config", "foo.config", "foo.bar.config")]
        public void CaseSensitiveOnNix(string pattern, string from, string to)
        {
            if (!CalamariEnvironment.IsRunningOnNix)
                Assert.Ignore("This test is designed to run on *nix");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, pattern);

            CreateConvention(deployment.Variables).Install(deployment);

            AssertTransformNotRun(from, to);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldOutputDiagnosticsLoggingIfEnabled()
        {
            var calamariFileSystem = Substitute.For<ICalamariFileSystem>();
            var deploymentVariables = new CalamariVariables();
            deploymentVariables.Set(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            deploymentVariables.Set(SpecialVariables.Action.Azure.CloudServicePackagePath, @"MyPackage.1.0.0.nupkg");
            deploymentVariables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, @"MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config => MyApplication.ProcessingServer.WorkerRole.dll.config");
            deploymentVariables.Set(DeploymentEnvironment.Name, "my-test-env");
            deploymentVariables.Set(SpecialVariables.Package.EnableDiagnosticsConfigTransformationLogging, "True");
            deploymentVariables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.ConfigurationTransforms);
            var runningDeployment = new RunningDeployment(@"c:\temp\MyPackage.1.0.0.nupkg", deploymentVariables);

            //mock the world
            calamariFileSystem.DirectoryExists(@"c:\temp").Returns(true);
            calamariFileSystem.FileExists(@"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config").Returns(true);
            calamariFileSystem.FileExists(@"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.config").Returns(true);
            calamariFileSystem.EnumerateFilesRecursively(@"c:\temp", "*.config")
                .Returns(new[] { @"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config", @"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.config" });
            calamariFileSystem.EnumerateFiles(@"c:\temp", "MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config", @"MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config")
                .Returns(new[] { @"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config" });
            calamariFileSystem.EnumerateFiles(@"c:\temp", "MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config", @"MyApplication.ProcessingServer.WorkerRole.dll.my-test-env")
             .Returns(new[] { @"c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config" });

            //these variables would normally be set by ExtractPackageToStagingDirectoryConvention
            Log.SetOutputVariable(PackageVariables.Output.InstallationDirectoryPath, "c:\\temp", runningDeployment.Variables);
            Log.SetOutputVariable(KnownVariables.OriginalPackageDirectoryPath, "c:\\temp", runningDeployment.Variables);

            var log = new InMemoryLog();
            var transformer = Substitute.For<IConfigurationTransformer>();
            var fileLocator = new TransformFileLocator(calamariFileSystem, log);
            new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(calamariFileSystem, deploymentVariables, transformer, fileLocator, log)).Install(runningDeployment);

            //not completely testing every scenario here, but this is a reasonable half way point to make sure it works without going overboard
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @"Recursively searching for transformation files that match *.config in folder 'c:\temp'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @"Found config file 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config => MyApplication.ProcessingServer.WorkerRole.dll.config'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - Skipping as file name 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config' does not match the target pattern 'MyApplication.ProcessingServer.WorkerRole.dll.config'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'Release'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - skipping as neither transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.Release.config' nor transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.Release' could be found in 'c:\temp'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'my-test-env'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - skipping as neither transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.my-test-env.config' nor transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.my-test-env' could be found in 'c:\temp'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @"Found config file 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.config'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config => MyApplication.ProcessingServer.WorkerRole.dll.config'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Info    && m.FormattedMessage == @"Transforming 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.config' using 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config'.");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'Release'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - skipping as neither transform 'MyApplication.ProcessingServer.WorkerRole.dll.Release.config' nor transform 'MyApplication.ProcessingServer.WorkerRole.dll.Release' could be found in 'c:\temp'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - checking against transform 'my-test-env'");
            log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage == @" - Skipping as target 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.config' has already been transformed by transform 'c:\temp\MyApplication.ProcessingServer.WorkerRole.dll.my-test-env.config'");
        }

        private static IEnumerable AdvancedTransformTestCases
        {
            get
            {
                //get absolute path and test against that too
                var directory = BuildConfigPath("") + Path.DirectorySeparatorChar;
                yield return new TestCaseData("bar.sitemap", "config\\fizz.sitemap.config=>bar.sitemap", "config\\fizz.sitemap.config");
                yield return new TestCaseData("bar.config", "config\\fizz.buzz.config=>bar.config", "config\\fizz.buzz.config");
                yield return new TestCaseData("bar.config", "foo.config=>bar.config", "foo.config");
                yield return new TestCaseData("bar.blah", "foo.baz=>bar.blah", "foo.baz");
                yield return new TestCaseData("bar.config", "foo.xml=>bar.config", "foo.xml");
                yield return new TestCaseData("xyz.bar.blah", "*.foo.blah=>*.bar.blah", "xyz.foo.blah");
                yield return new TestCaseData("foo.bar.config", "foo.config=>*.bar.config", "foo.config");
                yield return new TestCaseData("bar.blah", "*.bar.config=>bar.blah", "foo.bar.config");
                yield return new TestCaseData("foo.config", "foo.bar.additional.config=>foo.config", "foo.bar.additional.config");
                yield return new TestCaseData("foo.config", "*.bar.config=>*.config", "foo.bar.config");
                yield return new TestCaseData("foo.xml", "*.bar.xml=>*.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", "foo.bar.xml=>config\\fizz.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\fizz.buzz.xml=>config\\fizz.xml", "transform\\fizz.buzz.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\*.xml=>config\\*.xml", "transform\\fizz.xml");
                yield return new TestCaseData("foo.config", "transform\\*.config=>foo.config", "transform\\fizz.config");
                yield return new TestCaseData("bar.sitemap", directory + "config\\fizz.sitemap.config=>bar.sitemap", "config\\fizz.sitemap.config");
                yield return new TestCaseData("bar.config", directory + "config\\fizz.buzz.config=>bar.config", "config\\fizz.buzz.config");
                yield return new TestCaseData("bar.config", directory + "foo.config=>bar.config", "foo.config");
                yield return new TestCaseData("bar.blah", directory + "foo.baz=>bar.blah", "foo.baz");
                yield return new TestCaseData("bar.config", directory + "foo.xml=>bar.config", "foo.xml");
                yield return new TestCaseData("xyz.bar.blah", directory + "*.foo.blah=>*.bar.blah", "xyz.foo.blah");
                yield return new TestCaseData("foo.bar.config", directory + "foo.config=>*.bar.config", "foo.config");
                yield return new TestCaseData("bar.blah", directory + "*.bar.config=>bar.blah", "foo.bar.config");
                yield return new TestCaseData("foo.config", directory + "foo.bar.additional.config=>foo.config", "foo.bar.additional.config");
                yield return new TestCaseData("foo.config", directory + "*.bar.config=>*.config", "foo.bar.config");
                yield return new TestCaseData("foo.xml", directory + "*.bar.xml=>*.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", directory + "foo.bar.xml=>config\\fizz.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", directory + "transform\\fizz.buzz.xml=>config\\fizz.xml", "transform\\fizz.buzz.xml");
                yield return new TestCaseData("config\\fizz.xml", directory + "transform\\*.xml=>config\\*.xml", "transform\\fizz.xml");
                yield return new TestCaseData("foo.config", directory + "transform\\*.config=>foo.config", "transform\\fizz.config");
            }
        }

        private ConfigurationTransformsConvention CreateConvention(IVariables variables)
        {
            return new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables, configurationTransformer, transformFileLocator, logs));
        }

        private void AssertTransformRun(string configFile, string transformFile)
        {
            configurationTransformer.Received().PerformTransform(
                Arg.Is<string>(s => s.Equals(BuildConfigPath(configFile), StringComparison.OrdinalIgnoreCase)),
                Arg.Is<string>(s => s.Equals(BuildConfigPath(transformFile), StringComparison.OrdinalIgnoreCase)),
                Arg.Is<string>(s => s.Equals(BuildConfigPath(configFile), StringComparison.OrdinalIgnoreCase)));
        }

        private void AssertTransformNotRun(string configFile, string transformFile)
        {
            configurationTransformer.DidNotReceive().PerformTransform(
                Arg.Is<string>(s => s.Equals(BuildConfigPath(configFile), StringComparison.OrdinalIgnoreCase)),
                Arg.Is<string>(s => s.Equals(BuildConfigPath(transformFile), StringComparison.OrdinalIgnoreCase)),
                Arg.Is<string>(s => s.Equals(BuildConfigPath(configFile), StringComparison.OrdinalIgnoreCase)));
        }

        private static string BuildConfigPath(string filename)
        {
            var path = typeof(ConfigurationTransformConventionFixture).Namespace.Replace("Calamari.Tests.", string.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            var workingDirectory = Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, "ConfigTransforms");

            if (string.IsNullOrEmpty(filename))
                return workingDirectory;

            return Path.Combine(workingDirectory, filename.Replace('\\', Path.DirectorySeparatorChar));
        }
    }
}
