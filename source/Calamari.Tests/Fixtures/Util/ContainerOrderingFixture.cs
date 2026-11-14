using System.Collections.Generic;
using System.Linq;
using Autofac;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;
using KubernetesSpecialVariables = Calamari.Kubernetes.SpecialVariables;
using DeploymentSpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.Fixtures.Util
{
    /// <summary>
    /// Guards the orderings that the DI container is responsible for producing.
    ///
    /// Script wrappers form an execution chain and file-format replacers are tried in sequence,
    /// so a change in order is a change in deployment behaviour — but every wrapper still
    /// constructs and every command still resolves, so nothing else in the suite would notice.
    /// </summary>
    [TestFixture]
    public class ContainerOrderingFixture
    {
        IContainer container;

        [SetUp]
        public void SetUp()
        {
            container = TestableSyncProgram.For<Calamari.Program>().BuildTestContainer();
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        /// <summary>
        /// This is the invariant that makes the wrapper chain independent of the DI container.
        ///
        /// ScriptEngine orders wrappers with OrderByDescending(Priority), which is a *stable* sort,
        /// so any two wrappers sharing a priority fall back to the order the container happened to
        /// return them in. While every priority is distinct the chain is fully determined by
        /// ScriptWrapperPriorities; introduce a tie and it silently becomes container-dependent.
        /// </summary>
        [Test]
        [Category("PlatformAgnostic")]
        public void ScriptWrapperPrioritiesAreUnique()
        {
            var wrappers = container.Resolve<IEnumerable<IScriptWrapper>>().ToList();

            wrappers.Should().NotBeEmpty("the flavour is expected to register script wrappers");

            var duplicates = wrappers.GroupBy(w => w.Priority)
                                     .Where(g => g.Count() > 1)
                                     .Select(g => $"priority {g.Key}: {string.Join(", ", g.Select(w => w.GetType().Name))}")
                                     .ToList();

            duplicates.Should()
                      .BeEmpty("wrappers sharing a priority make the execution chain depend on the DI container's "
                               + "collection ordering rather than on ScriptWrapperPriorities");
        }

        /// <summary>
        /// Pins the full chain for a representative step that enables every wrapper at once:
        /// a Kubernetes deployment, authenticated with an AWS account, running PowerShell,
        /// with resource status checking on.
        /// </summary>
        [Test]
        [Category("PlatformAgnostic")]
        public void ScriptWrapperChain_ForKubernetesAwsPowerShellStep_IsOrderedByDescendingPriority()
        {
            var variables = container.Resolve<IVariables>();

            // Kubernetes step: enables KubernetesContextScriptWrapper and ManifestReportScriptWrapper
            variables.Set(KubernetesSpecialVariables.ClusterUrl, "https://cluster.example");
            // ...and ResourceStatusReportScriptWrapper, provided it is not a blue/green or wait deployment
            variables.Set(KubernetesSpecialVariables.ResourceStatusCheck, "True");
            // AWS authentication: enables AwsScriptWrapper
            variables.Set(DeploymentSpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            // Script function registration: enables FunctionAppenderScriptWrapper for supported syntaxes.
            // Literal because ScriptFunctionsVariables is internal to Calamari.Common.
            variables.Set("Octopus.Sashimi.ScriptFunctions.Registration", "SomeRegistration");

            var chain = ResolveChain(ScriptSyntax.PowerShell);

            chain.Should()
                 .Equal(new[]
                        {
                            nameof(Calamari.Kubernetes.ResourceStatus.ResourceStatusReportScriptWrapper), // 1003
                            nameof(Calamari.Kubernetes.ManifestReportScriptWrapper),                      // 1002
                            nameof(Calamari.Kubernetes.KubernetesContextScriptWrapper),                   // 1001
                            nameof(Calamari.Aws.Integration.AwsScriptWrapper),                            // 1000
                            nameof(Calamari.Common.Features.FunctionScriptContributions.FunctionAppenderScriptWrapper) // 100
                        },
                        "the wrapper chain determines the order authentication and tooling are applied around a script");
        }

        /// <summary>
        /// With no variables set no wrapper should opt in, so a plain script runs unwrapped.
        /// </summary>
        [Test]
        [Category("PlatformAgnostic")]
        public void ScriptWrapperChain_ForAPlainScript_IsEmpty()
        {
            ResolveChain(ScriptSyntax.PowerShell).Should().BeEmpty();
        }

        /// <summary>
        /// Structured configuration variables try each replacer in turn, so this order decides
        /// which format wins for a file that more than one replacer would accept.
        /// </summary>
        [Test]
        [Category("PlatformAgnostic")]
        public void FileFormatVariableReplacers_ResolveInDeclaredPriorityOrder()
        {
            using var scope = container.BeginLifetimeScope();

            var replacers = scope.Resolve<PrioritisedList<IFileFormatVariableReplacer>>()
                                 .Select(r => r.GetType().Name);

            replacers.Should()
                     .Equal(nameof(JsonFormatVariableReplacer),
                            nameof(XmlFormatVariableReplacer),
                            nameof(YamlFormatVariableReplacer),
                            nameof(PropertiesFormatVariableReplacer));
        }

        /// <summary>
        /// KubernetesDiscovererFactory builds a dictionary keyed on Type, so a duplicate key
        /// throws at construction and a reordering would change which discoverer wins.
        /// </summary>
        [Test]
        [Category("PlatformAgnostic")]
        public void KubernetesDiscoverers_HaveDistinctTypeKeys()
        {
            var discoverers = container.Resolve<IEnumerable<IKubernetesDiscoverer>>().ToList();

            discoverers.Should().NotBeEmpty();
            discoverers.Select(d => d.Type).Should().OnlyHaveUniqueItems();
        }

        /// <summary>
        /// Mirrors the enabled-and-ordered selection ScriptEngine.BuildWrapperChain performs
        /// before folding the wrappers into a linked list.
        /// </summary>
        List<string> ResolveChain(ScriptSyntax syntax)
            => container.Resolve<IEnumerable<IScriptWrapper>>()
                        .Where(w => w.IsEnabled(syntax))
                        .OrderByDescending(w => w.Priority)
                        .Select(w => w.GetType().Name)
                        .ToList();
    }
}
