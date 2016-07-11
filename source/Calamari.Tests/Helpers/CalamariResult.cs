using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ApprovalTests;
using Calamari.Integration.ServiceMessages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Calamari.Tests.Helpers
{
    public class CalamariResult
    {
        private readonly int exitCode;
        private readonly CaptureCommandOutput captured;

        public CalamariResult(int exitCode, CaptureCommandOutput captured)
        {
            this.exitCode = exitCode;
            this.captured = captured;
        }

        private int ExitCode
        {
            get { return exitCode; }
        }

        public CaptureCommandOutput CapturedOutput { get { return captured; } }

        public void AssertSuccess()
        {
            var capturedErrors = string.Join(Environment.NewLine, captured.Errors);
            Assert.That(ExitCode, Is.EqualTo(0), string.Format("Expected command to return exit code 0{0}{0}Output:{0}{1}", Environment.NewLine, capturedErrors));
        }

        public void AssertNonZero()
        {
            Assert.That(ExitCode, Is.Not.EqualTo(0), "Expected a non-zero exit code");
        }


        public void AssertNonZero(int code)
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

        public void AssertServiceMessage(string name, IResolveConstraint resolveConstraint = null, Dictionary<string, object> properties = null, string message = "", params object[] args)
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

        static void AssertServiceMessageValue(string property, object expected, string actual, IResolveConstraint resolveConstraint)
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

            Assert.That(allOutput.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) == -1, string.Format("Expected not to find: {0}. Output:\r\n{1}", expectedOutput, allOutput));
        }

        public void AssertOutput(string expectedOutput)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Infos);

            Assert.That(allOutput.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) >= 0, string.Format("Expected to find: {0}. Output:\r\n{1}", expectedOutput, allOutput));
        }

        public void AssertOutput(Regex regex)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Infos);

            Assert.That(regex.IsMatch(allOutput),
                string.Format("Output did not match: {0}. Output:\r\n{1}", regex.ToString(), allOutput) );
        }

        public string GetOutputForLineContaining(string expectedOutput)
        {
            var found = captured.Infos.SingleOrDefault(i => i.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNotNull(found);
            return found;
        }

        public void AssertErrorOutput(string expectedOutputFormat, params object[] args)
        {
            AssertErrorOutput(String.Format(expectedOutputFormat, args));
        }

        public void AssertErrorOutput(string expectedOutput)
        {
            var allOutput = string.Join(Environment.NewLine, captured.Errors);
            Assert.That(allOutput.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) >= 0, string.Format("Expected to find: {0}. Output:\r\n{1}", expectedOutput, allOutput));
        }

        public void ApproveOutput()
        {
            Approvals.Verify(captured.ToApprovalString());
        }
    }
}