using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Testing.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Integration
{
    [TestFixture]
    public class HelmCliTests
    {
        [Test]
        public void ExecutesWithBuiltInArguments()
        {
            const string expectedExecutable = "some-exe";
            const string expectedNamespace = "some-namespace";
            const string expectedArgument = "additional-arg";

            var (helm, commandLineRunner, _) = GetHelmCli();
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());

            helm.WithExecutable(expectedExecutable);
            helm.WithNamespace(expectedNamespace);

            helm.ExecuteCommandAndReturnOutput(expectedArgument);

            using (var _ = new AssertionScope())
            {
                actual.Executable.Should().BeEquivalentTo(expectedExecutable);
                actual.Arguments.Should().BeEquivalentTo($"--namespace {expectedNamespace} {expectedArgument}");
            }
        }

        [Test]
        public void UsesCustomHelmExecutable()
        {
            var (helm, commandLineRunner, _) = GetHelmCli();
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());

            const string expectedExecutable = "my-custom-exe";

            helm.WithExecutable(new CalamariVariables
            {
                { SpecialVariables.Helm.CustomHelmExecutable, expectedExecutable }
            });

            helm.ExecuteCommandAndReturnOutput();

            actual.Executable.Should().BeEquivalentTo(expectedExecutable);
        }

        [Test]
        public void UsesCustomHelmExecutableFromPackage()
        {
            var (helm, commandLineRunner, workingDirectory) = GetHelmCli();
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());

            const string expectedExecutable = "my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";
            var expectedExecutablePath = Path.Combine(workingDirectory.DirectoryPath, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, expectedExecutable);

            helm.WithExecutable(new CalamariVariables
            {
                { SpecialVariables.Helm.CustomHelmExecutable, expectedExecutable },
                { SpecialVariables.Helm.Packages.CustomHelmExePackageKey, expectedPackageKey },
                { $"{PackageVariables.PackageCollection}[{expectedPackageKey}]", SpecialVariables.Helm.Packages.CustomHelmExePackageKey }
            });

            helm.ExecuteCommandAndReturnOutput();

            actual.Executable.Should().BeEquivalentTo(expectedExecutablePath);
        }

        [Test]
        public void AlwaysUsesCustomHelmExecutableWhenRooted()
        {
            var (helm, commandLineRunner, _) = GetHelmCli();
            CommandLineInvocation actual = null;
            commandLineRunner.When(x => x.Execute(Arg.Any<CommandLineInvocation>())).Do(x => actual = x.Arg<CommandLineInvocation>());

            const string expectedExecutable = "/my-custom-exe";
            const string expectedPackageKey = "helm-exe-package";

            helm.WithExecutable(new CalamariVariables
            {
                { SpecialVariables.Helm.CustomHelmExecutable, expectedExecutable },
                { SpecialVariables.Helm.Packages.CustomHelmExePackageKey, expectedPackageKey },
                { $"{PackageVariables.PackageCollection}[{expectedPackageKey}]", SpecialVariables.Helm.Packages.CustomHelmExePackageKey }
            });

            helm.ExecuteCommandAndReturnOutput();

            actual.Executable.Should().BeEquivalentTo(expectedExecutable);
        }

        static (HelmCli, ICommandLineRunner, TemporaryDirectory) GetHelmCli()
        {
            var memoryLog = new InMemoryLog();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var workingDirectory = TemporaryDirectory.Create();
            var helm = new HelmCli(memoryLog, commandLineRunner, workingDirectory.DirectoryPath, new Dictionary<string, string>());

            return (helm, commandLineRunner, workingDirectory);
        }
    }
}