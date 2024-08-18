using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.ServiceMessages;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Calamari.Testing.Helpers
{
    public class CalamariResult
    {
        private readonly int exitCode;
        private readonly CaptureCommandInvocationOutputSink captured;

        public CalamariResult(int exitCode, CaptureCommandInvocationOutputSink captured)
        {
            this.exitCode = exitCode;
            this.captured = captured;
        }

        public int ExitCode
        {
            get { return exitCode; }
        }

        public CaptureCommandInvocationOutputSink CapturedOutput
        {
            get { return captured; }
        }

        public void AssertSuccess()
        {
            var capturedErrors = string.Join(Environment.NewLine, captured.Errors);
            Assert.That(ExitCode, Is.EqualTo(0), string.Format("Expected command to return exit code 0, instead returned {2}{0}{0}Output:{0}{1}", Environment.NewLine, capturedErrors, ExitCode));
        }

        public void AssertFailure()
        {
            Assert.That(ExitCode, Is.Not.EqualTo(0), "Expected a non-zero exit code");
        }


        public void AssertFailure(int code)
        {
            Assert.That(ExitCode, Is.EqualTo(code), $"Expected an exit code of {code}");
        }

        public void AssertOutput(string expectedOutputFormat, params object[] args)
        {
            AssertOutput(String.Format(expectedOutputFormat, args));
        }

        public void AssertOutputVariable(string name, IResolveConstraint resolveConstraint)
        {
            var variable = captured.OutputVariables.Get(name);
            Assert.That(variable, resolveConstraint);
        }

        public void AssertServiceMessage(string name, IResolveConstraint? resolveConstraint = null, Dictionary<string, object>? properties = null, string message = "", params object[] args)
        {
            switch (name)
            {
                case ServiceMessageNames.CalamariFoundPackage.Name:
                    Assert.That(captured.CalamariFoundPackage, resolveConstraint, message, args);
                    break;
                case ServiceMessageNames.FoundPackage.Name:
                    Assert.That(captured.FoundPackage, Is.Not.Null);
                    if (properties != null)
                    {
                        Assert.That(resolveConstraint, Is.Not.Null, "Resolve constraint was not provided");
                        foreach (var property in properties)
                        {
                            var fp = JObject.FromObject(captured.FoundPackage);
                            string value;
                            if (property.Key.Contains("."))
                            {
                                var props = property.Key.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
                                value = fp[props[0]][props[1]].ToString();
                            }
                            else
                            {
                                value = fp[property.Key].ToString();
                            }

                            AssertServiceMessageValue(property.Key, property.Value, value, resolveConstraint);
                        }
                    }

                    break;
                case ServiceMessageNames.PackageDeltaVerification.Name:
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        Assert.That(captured?.DeltaError?.Replace("\r\n", "\n"), Is.EqualTo(message));
                        break;
                    }

                    Assert.That(captured.DeltaVerification, Is.Not.Null);
                    if (properties != null)
                    {
                        foreach (var property in properties)
                        {
                            var dv = JObject.FromObject(captured.DeltaVerification);
                            string value;
                            if (property.Key.Contains("."))
                            {
                                var props = property.Key.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
                                value = dv[props[0]][props[1]].ToString();
                            }
                            else
                            {
                                value = dv[property.Key].ToString();
                            }

                            AssertServiceMessageValue(property.Key, property.Value, value, resolveConstraint);
                        }
                    }

                    break;
            }
        }

        static void AssertServiceMessageValue(string property, object expected, string actual, IResolveConstraint? resolveConstraint)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual, Is.Not.Empty);
            Assert.That(actual.Equals(expected), resolveConstraint,
                "Expected property '{0}' to have value '{1}' but was actually '{2}'", property,
                expected, actual);
        }

        public void AssertNoOutput(string expectedOutput)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Infos);

            Assert.That(allOutput, Does.Not.Contain(expectedOutput));
        }

        public void AssertOutput(string expectedOutput)
        {
            var allOutput = string.Join(Environment.NewLine, captured.AllMessages);

            Assert.That(allOutput, Does.Contain(expectedOutput));
        }

        public void AssertOutputContains(string expected)
        {
            var allOutput = string.Join(Environment.NewLine, captured.AllMessages);

            allOutput.Should().Contain(expected);
        }

        public void AssertOutputMatches(string regex, string? message = null)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Infos);

            Assert.That(allOutput, Does.Match(regex), message);
        }

        public void AssertNoOutputMatches(string regex)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Infos);

            Assert.That(allOutput, Does.Not.Match(regex));
        }

        public string GetOutputForLineContaining(string expectedOutput)
        {
            var found = captured.Infos.SingleOrDefault(i => i.ContainsIgnoreCase(expectedOutput));
            found.Should().NotBeNull($"'{expectedOutput}' should exist");
            return found;
        }

        public void AssertErrorOutput(string expectedOutputFormat, params object[] args)
        {
            AssertErrorOutput(String.Format(expectedOutputFormat, args));
        }

        public void AssertErrorOutput(string expectedOutput, bool noNewLines = false)
        {
            var separator = noNewLines ? String.Empty : Environment.NewLine;
            var allOutput = string.Join(separator, captured.Errors);
            Assert.That(allOutput, Does.Contain(expectedOutput));
        }

        //Assuming we print properties like:
        //"name: expectedValue"
        public void AssertPropertyValue(string name, params string[] expectedValues)
        {
            var title = name + ":";
            string line = GetOutputForLineContaining(title);

            line.Replace(title, "").Should().BeOneOf(expectedValues);
        }

        public void AssertProcessNameAndId(string processName)
        {
            AssertOutputMatches(@"HostProcess: (Calamari|dotnet|mono-sgen32|mono-sgen64|mono-sgen) \([0-9]+\)", "Calamari process name and id are printed");
            AssertOutputMatches($@"HostProcess: ({processName}|mono-sgen32|mono-sgen64|mono-sgen) \([0-9]+\)", $"{processName} process name and id are printed");
        }
    }
}