#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Autofac;
using Autofac.Core;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    /// <summary>
    /// Compares a rendering of the whole production container against an approved snapshot.
    ///
    /// Targeted assertions only catch what someone thought to assert. This catches anything
    /// observable that moves — a registration appearing or vanishing, a lifetime changing, a
    /// different implementation being injected — which is what makes it useful across a
    /// dependency upgrade, where you cannot predict in advance what will shift.
    ///
    /// To re-approve after an intended change, run with CALAMARI_APPROVE_CONTAINER_SNAPSHOT=1
    /// and review the resulting diff.
    /// </summary>
    [TestFixture]
    public class ContainerSnapshotFixture
    {
        // Deliberately not named *.approved.* — .gitattributes marks that pattern binary,
        // which would hide the diff this test exists to surface in review.
        const string ExpectedFileName = "ContainerSnapshot.expected.txt";
        const string ApprovalEnvironmentVariable = "CALAMARI_APPROVE_CONTAINER_SNAPSHOT";

        /// <summary>
        /// Implementations chosen by the host OS rather than by the container. Collapsed to a
        /// placeholder so one approved file serves every platform the tests run on.
        /// </summary>
        static readonly (string Concrete, string Placeholder)[] PlatformSpecificTypes =
        {
            ("NixCalamariPhysicalFileSystem", "<PhysicalFileSystem>"),
            ("WindowsPhysicalFileSystem", "<PhysicalFileSystem>"),
            ("KubernetesFileSystem", "<PhysicalFileSystem>"),
            ("WindowsX509CertificateStore", "<WindowsX509CertificateStore>"),
            ("NoOpWindowsX509CertificateStore", "<WindowsX509CertificateStore>")
        };

        [Test]
        [Category("PlatformAgnostic")]
        public void ContainerMatchesApprovedSnapshot()
        {
            var actual = Normalise(CaptureSnapshot());

            if (Environment.GetEnvironmentVariable(ApprovalEnvironmentVariable) == "1")
            {
                File.WriteAllText(ExpectedFileSourcePath(), actual);
                Assert.Inconclusive($"Snapshot re-approved. Review the diff in {ExpectedFileName} before committing.");
            }

            var expected = ReadExpected();

            if (actual == expected)
                return;

            var actualPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ContainerSnapshot.actual.txt");
            File.WriteAllText(actualPath, actual);

            actual.Should()
                  .Be(expected,
                      $"the container should match {ExpectedFileName}. Actual written to {actualPath}. "
                      + $"If the change is intended, re-run with {ApprovalEnvironmentVariable}=1 and review the diff.");
        }

        static string CaptureSnapshot()
        {
            var sb = new StringBuilder();
            using var container = TestableSyncProgram.For<Calamari.Program>().BuildTestContainer();

            sb.AppendLine("## AUTOFAC");
            sb.AppendLine(typeof(ContainerBuilder).Assembly.GetName().Version!.ToString());
            sb.AppendLine();

            sb.AppendLine("## REGISTRATIONS");
            foreach (var line in container.ComponentRegistry.Registrations.Select(DescribeRegistration).OrderBy(l => l, StringComparer.Ordinal))
                sb.AppendLine(line);
            sb.AppendLine();

            sb.AppendLine("## COLLECTION RESOLUTION ORDER");
            DumpCollection<Calamari.Common.Features.Scripting.IScriptWrapper>(container, sb);
            DumpCollection<Calamari.Common.Features.FunctionScriptContributions.ICodeGenFunctions>(container, sb);
            DumpCollection<Calamari.Common.Features.Discovery.IKubernetesDiscoverer>(container, sb);
            DumpCollection<Calamari.Common.Features.StructuredVariables.IFileFormatVariableReplacer>(container, sb);
            DumpCollection<ICommandWithArgs>(container, sb);
            sb.AppendLine();

            sb.AppendLine("## COMMANDS: metadata + injected concrete types");
            var commands = container.Resolve<IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>>>()
                                    .OrderBy(c => c.Metadata.Name, StringComparer.Ordinal);
            foreach (var command in commands)
            {
                string line;
                try
                {
                    var instance = command.Value.Value;
                    line = $"{command.Metadata.Name} => {Name(instance.GetType())} {{ {DescribeFields(instance)} }}";
                }
                catch (Exception ex)
                {
                    line = $"{command.Metadata.Name} => THREW {ex.GetType().Name}";
                }

                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine("## PIPELINE COMMANDS (named registrations)");
            foreach (var name in container.ComponentRegistry.Registrations
                                          .SelectMany(r => r.Services)
                                          .OfType<KeyedService>()
                                          .Where(k => k.ServiceType == typeof(PipelineCommand))
                                          .Select(k => k.ServiceKey.ToString())
                                          .OrderBy(n => n, StringComparer.Ordinal))
                sb.AppendLine(name);

            return sb.ToString();
        }

        static string DescribeRegistration(IComponentRegistration registration)
        {
            var services = string.Join("+", registration.Services.Select(s => s.Description).OrderBy(s => s, StringComparer.Ordinal));

            // __RegistrationOrder is an Autofac-internal tick counter and differs on every run.
            var metadata = string.Join(",",
                                       registration.Metadata
                                                   .Where(kv => kv.Key != "__RegistrationOrder")
                                                   .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                                   .Select(kv => $"{kv.Key}={Describe(kv.Value)}"));

            return $"{Name(registration.Activator.LimitType)} :: {services} :: "
                   + $"{registration.Sharing}/{registration.Lifetime.GetType().Name}/{registration.Ownership} :: [{metadata}]";
        }

        static void DumpCollection<T>(IComponentContext container, StringBuilder sb)
        {
            try
            {
                var items = container.Resolve<IEnumerable<T>>().Select(x => Name(x!.GetType()));
                sb.AppendLine($"{typeof(T).Name}: {string.Join(", ", items)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{typeof(T).Name}: THREW {ex.GetType().Name}");
            }
        }

        static string DescribeFields(object instance)
        {
            var fields = instance.GetType()
                                 .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                 .Where(f => !f.IsLiteral)
                                 .OrderBy(f => f.Name, StringComparer.Ordinal);

            var parts = new List<string>();
            foreach (var field in fields)
            {
                object? value;
                try
                {
                    value = field.GetValue(instance);
                }
                catch
                {
                    value = "<unreadable>";
                }

                parts.Add($"{field.Name}:{Describe(value)}");
            }

            return string.Join(", ", parts);
        }

        static string Describe(object? value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return s;
                case Enum e:
                    return e.ToString();
            }

            if (value is IEnumerable enumerable)
            {
                // Distinct element types only. Element counts vary with the ambient environment
                // (IVariables carries environment variables) and would swamp the diff.
                var elementTypes = enumerable.Cast<object?>()
                                             .Select(x => x is null ? "null" : Name(x.GetType()))
                                             .Distinct()
                                             .OrderBy(n => n, StringComparer.Ordinal);
                return $"{Name(value.GetType())}[{string.Join("|", elementTypes)}]";
            }

            return value.GetType().IsPrimitive ? value.ToString()! : Name(value.GetType());
        }

        static string Name(Type type)
            => type.IsGenericType
                ? $"{type.Name.Split('`')[0]}<{string.Join(",", type.GetGenericArguments().Select(Name))}>"
                : type.Name;

        static string Normalise(string snapshot)
            => PlatformSpecificTypes.Aggregate(snapshot.Replace("\r\n", "\n"),
                                               (current, mapping) => current.Replace(mapping.Concrete, mapping.Placeholder));

        static string ReadExpected()
        {
            var assembly = typeof(ContainerSnapshotFixture).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                                       .SingleOrDefault(n => n.EndsWith(ExpectedFileName, StringComparison.Ordinal));

            resourceName.Should().NotBeNull($"{ExpectedFileName} should be embedded in {assembly.GetName().Name}");

            using var stream = assembly.GetManifestResourceStream(resourceName!)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Replace("\r\n", "\n");
        }

        static string ExpectedFileSourcePath([CallerFilePath] string callerFilePath = "")
            => Path.Combine(Path.GetDirectoryName(callerFilePath)!, ExpectedFileName);
    }
}
