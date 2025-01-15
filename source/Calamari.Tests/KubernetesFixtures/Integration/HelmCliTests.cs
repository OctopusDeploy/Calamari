using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Integration
{
    [TestFixture]
    public class HelmCliTests
    {
        const string BashScriptFilename = "Calamari.HelmUpgrade.sh";
        readonly CalamariVariables executeViaScriptFeatureToggle = new CalamariVariables { [KnownVariables.EnabledFeatureToggles] = OctopusFeatureToggles.KnownSlugs.ExecuteHelmUpgradeCommandViaShellScript };
        
        [Test]
        public void ExecutesWithDefaultExecutable()
        {
            var (helm, commandLineRunner, _, _) = GetHelmCli();
            CommandLineInvocation actual = null;
            commandLineRunner
                .When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                .Do(x => actual = x.Arg<CommandLineInvocation>());

            helm.GetExecutableVersion();

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo("helm");
                actual.Arguments.Should().BeEquivalentTo($"version --client --short");
            }
        }

        [Test]
        public void UsesCustomHelmExecutable()
        {
            const string expectedExecutable = "my-custom-exe";

            var (helm, commandLineRunner, _, _) = GetHelmCli(expectedExecutable);
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                             .Do(x => actual = x.Arg<CommandLineInvocation>());

            helm.GetExecutableVersion();

            actual.Executable.Should().BeEquivalentTo(expectedExecutable);
        }
        
        [Test]
        [NonWindowsTest]
        public void ChmodsCustomHelmExecutableWhenNotOnWindows()
        {
            const string expectedExecutable = "my-custom-exe";

            var (helm, commandLineRunner, _, _) = GetHelmCli(expectedExecutable);
            IList<CommandLineInvocation> invocations = new List<CommandLineInvocation>();
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                             .Do(x => invocations.Add(x.Arg<CommandLineInvocation>()));

            helm.GetExecutableVersion();

            invocations.Should().Contain(cli => cli.Executable == expectedExecutable);
            invocations.Should().Contain(cli => cli.Executable == "chmod" && cli.Arguments == $"+x {expectedExecutable}");
        }

        [Test]
        public void UsesCustomHelmExecutableFromPackage()
        {
            const string expectedExecutable = "my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";

            var (helm, commandLineRunner, workingDirectory, _) = GetHelmCli(expectedExecutable, new CalamariVariables
            {
                [$"{PackageVariables.PackageCollection}[{expectedPackageKey}]"] = SpecialVariables.Helm.Packages.CustomHelmExePackageKey
            });
            
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());

            var expectedExecutablePath = Path.Combine(workingDirectory.DirectoryPath, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, expectedExecutable);

            helm.GetExecutableVersion();

            actual.Executable.Should().BeEquivalentTo(expectedExecutablePath);
        }
        
        [Test]
        public void AlwaysUsesCustomHelmExecutableWhenRooted()
        {
            const string expectedExecutable = "/my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";
            
            var (helm, commandLineRunner, _, _) = GetHelmCli(expectedExecutable, new CalamariVariables
            {
                { $"{PackageVariables.PackageCollection}[{expectedPackageKey}]", SpecialVariables.Helm.Packages.CustomHelmExePackageKey }
            });
            
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());
        
            helm.GetExecutableVersion();

            actual.Executable.Should().BeEquivalentTo(expectedExecutable);
        }
        
        [Test]
        [NonWindowsTest]
        public void ExecuteViaScript_ExecutesWithDefaultExecutable()
        {
            var (helm, commandLineRunner, workingDirectory, _) = GetHelmCli(additionalVariables: executeViaScriptFeatureToggle);
            CommandLineInvocation actual = null;
            string scriptContents = null;
            commandLineRunner
                .When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                .Do(x =>
                    {
                        actual = x.Arg<CommandLineInvocation>();
                        scriptContents = File.ReadAllText(Path.Combine(workingDirectory.DirectoryPath, BashScriptFilename));
                    });

            helm.Upgrade("myReleaseName", "myPackagePath", new []{"--reset-values", "--set something='SomeValue'"});

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo("bash");
                actual.Arguments.Should().Contain(BashScriptFilename);
            }

            scriptContents.Should().Be("helm upgrade --install --reset-values --set something='SomeValue' myReleaseName myPackagePath");
        }
        
        [Test]
        [NonWindowsTest]
        public void ExecuteViaScript_UsesCustomHelmExecutable()
        {
            const string expectedExecutable = "my-custom-exe";
            
            var (helm, commandLineRunner, workingDirectory, _) = GetHelmCli(customHelmExe: expectedExecutable, additionalVariables: executeViaScriptFeatureToggle);
            CommandLineInvocation actual = null;
            string scriptContents = null;
            commandLineRunner
                .When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                .Do(x =>
                    {
                        actual = x.Arg<CommandLineInvocation>();
                        scriptContents = File.ReadAllText(Path.Combine(workingDirectory.DirectoryPath, BashScriptFilename));
                    });

            helm.Upgrade("myReleaseName", "myPackagePath", new []{"--reset-values", "--set something='SomeValue'"});

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo("bash");
                actual.Arguments.Should().Contain(BashScriptFilename);
            }

            scriptContents.Should().Be($"chmod +x \"{expectedExecutable}\"; {expectedExecutable} upgrade --install --reset-values --set something='SomeValue' myReleaseName myPackagePath");
        }
        
        [Test]
        [NonWindowsTest]
        public void ExecuteViaScript_UsesCustomHelmExecutableFromPackage()
        {
            const string expectedExecutable = "my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";

            var variables = new CalamariVariables
            {
                [$"{PackageVariables.PackageCollection}[{expectedPackageKey}]"] = SpecialVariables.Helm.Packages.CustomHelmExePackageKey
            };
            variables.Merge(executeViaScriptFeatureToggle);

            var (helm, commandLineRunner, workingDirectory, _) = GetHelmCli(customHelmExe: expectedExecutable, additionalVariables: variables);
            
            CommandLineInvocation actual = null;
            string scriptContents = null;
            commandLineRunner
                .When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                .Do(x =>
                    {
                        actual = x.Arg<CommandLineInvocation>();
                        scriptContents = File.ReadAllText(Path.Combine(workingDirectory.DirectoryPath, BashScriptFilename));
                    });

            helm.Upgrade("myReleaseName", "myPackagePath", new []{"--reset-values", "--set something='SomeValue'"});

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo("bash");
                actual.Arguments.Should().Contain(BashScriptFilename);
            }
            
            var expectedExecutablePath = Path.Combine(workingDirectory.DirectoryPath, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, expectedExecutable);

            scriptContents.Should().Be($"chmod +x \"{expectedExecutablePath}\"; {expectedExecutablePath} upgrade --install --reset-values --set something='SomeValue' myReleaseName myPackagePath");
        }
        
        [Test]
        [NonWindowsTest]
        public void ExecuteViaScript_AlwaysUsesCustomHelmExecutableWhenRooted()
        {
            const string expectedExecutable = "/my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";

            var variables = new CalamariVariables
            {
                [$"{PackageVariables.PackageCollection}[{expectedPackageKey}]"] = SpecialVariables.Helm.Packages.CustomHelmExePackageKey
            };
            variables.Merge(executeViaScriptFeatureToggle);

            var (helm, commandLineRunner, workingDirectory, _) = GetHelmCli(customHelmExe: expectedExecutable, additionalVariables: variables);
            
            CommandLineInvocation actual = null;
            string scriptContents = null;
            commandLineRunner
                .When(x => x.Execute(Arg.Any<CommandLineInvocation>()))
                .Do(x =>
                    {
                        actual = x.Arg<CommandLineInvocation>();
                        scriptContents = File.ReadAllText(Path.Combine(workingDirectory.DirectoryPath, BashScriptFilename));
                    });

            helm.Upgrade("myReleaseName", "myPackagePath", new []{"--reset-values", "--set something='SomeValue'"});

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo("bash");
                actual.Arguments.Should().Contain(BashScriptFilename);
            }
            
            scriptContents.Should().Be($"chmod +x \"{expectedExecutable}\"; {expectedExecutable} upgrade --install --reset-values --set something='SomeValue' myReleaseName myPackagePath");
        }

        static (HelmCli, ICommandLineRunner, TemporaryDirectory, RunningDeployment) GetHelmCli(string customHelmExe = null, CalamariVariables additionalVariables = null)
        {
            var memoryLog = new InMemoryLog();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var workingDirectory = TemporaryDirectory.Create();
            var variables = new CalamariVariables();
            var fileSystem = new TestCalamariPhysicalFileSystem();

            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>())
                             .Returns(ci => new CommandResult(ci.ArgAt<CommandLineInvocation>(0).ToString(), 0));

            if (customHelmExe != null)
            {
                variables.Set(SpecialVariables.Helm.CustomHelmExecutable, customHelmExe);
            }

            if (additionalVariables != null)
            {
                variables.Merge(additionalVariables);
            }

            var runningDeployment = new RunningDeployment(variables, new Dictionary<string, string>())
            {
                StagingDirectory = workingDirectory.DirectoryPath
            };

            var helm = new HelmCli(memoryLog, commandLineRunner, runningDeployment, fileSystem);

            return (helm, commandLineRunner, workingDirectory, runningDeployment);
        }
    }
}