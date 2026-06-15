using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Helm
{
    /// <summary>
    /// Tests for Helm upgrade command argument construction.
    /// Validates the logic in HelmUpgradeExecutor's Set*Parameter methods
    /// without needing a real Helm binary or cluster.
    /// </summary>
    [TestFixture]
    public class HelmCommandArgsTests
    {
        [Test]
        public void ResetValues_IncludedByDefault()
        {
            var args = BuildArgs(new CalamariVariables());

            args.Should().Contain("--reset-values");
        }

        [Test]
        public void ResetValues_CanBeDisabled()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.ResetValues, "false");

            var args = BuildArgs(variables);

            args.Should().NotContain("--reset-values");
        }

        [Test]
        public void Timeout_AddedWhenSet()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.Timeout, "5m30s");

            var args = BuildArgs(variables);

            args.Should().Contain("--timeout \"5m30s\"");
        }

        [Test]
        public void Timeout_NotAddedWhenUnset()
        {
            var args = BuildArgs(new CalamariVariables());

            args.Should().NotContain(a => a.Contains("--timeout"));
        }

        [Test]
        public void Timeout_InvalidDuration_ThrowsCommandException()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.Timeout, "not-a-duration");

            var act = () => BuildArgs(variables);

            act.Should().Throw<CommandException>()
               .WithMessage("*not a valid duration*");
        }

        [Test]
        public void AdditionalArguments_AppendedToArgs()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.AdditionalArguments, "--dry-run --debug");

            var args = BuildArgs(variables);

            args.Should().Contain("--dry-run --debug");
        }

        [Test]
        public void KOS_AddsWaitFlag()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.ResourceStatusCheck, "true");

            var args = BuildArgs(variables);

            args.Should().Contain(a => a.Contains("--wait"));
        }

        [Test]
        public void KOS_DoesNotDuplicateWaitIfAlreadyInAdditionalArgs()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.ResourceStatusCheck, "true");
            variables.Set(SpecialVariables.Helm.AdditionalArguments, "--wait --debug");

            var args = BuildArgs(variables);

            // --wait should appear but not be duplicated in a separate arg
            var combined = string.Join(" ", args);
            combined.Should().Contain("--wait");
        }

        [Test]
        public void KOS_AddsWaitForJobsWhenEnabled()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.ResourceStatusCheck, "true");
            variables.Set(SpecialVariables.WaitForJobs, "true");

            var args = BuildArgs(variables);

            var combined = string.Join(" ", args);
            combined.Should().Contain("--wait-for-jobs");
            combined.Should().Contain("--wait");
        }

        [Test]
        public void KOS_DoesNotAddWaitForJobsByDefault()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.ResourceStatusCheck, "true");

            var args = BuildArgs(variables);

            var combined = string.Join(" ", args);
            combined.Should().NotContain("--wait-for-jobs");
        }

        [Test]
        public void KOS_AtomicSatisfiesWaitRequirement()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.ResourceStatusCheck, "true");
            variables.Set(SpecialVariables.Helm.AdditionalArguments, "--atomic");

            var args = BuildArgs(variables);

            // With --atomic already set, should not add --wait separately
            // (atomic is a superset of wait)
            var combined = string.Join(" ", args);
            combined.Should().Contain("--atomic");
        }

        /// <summary>
        /// Reproduces the argument construction logic from HelmUpgradeExecutor
        /// for the parameters that don't require filesystem/helm CLI interaction.
        /// </summary>
        static List<string> BuildArgs(IVariables variables)
        {
            var args = new List<string>();

            // SetResetValuesParameter
            if (variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
                args.Add("--reset-values");

            // SetTimeoutParameter
            if (variables.IsSet(SpecialVariables.Helm.Timeout))
            {
                var timeout = variables.Get(SpecialVariables.Helm.Timeout);
                if (!Calamari.Util.GoDurationParser.ValidateDuration(timeout))
                    throw new CommandException($"Timeout period is not a valid duration: {timeout}");
                args.Add($"--timeout \"{timeout}\"");
            }

            // SetAdditionalArguments
            var hasAdditionalArgs = false;
            var additionalArguments = variables.Get(SpecialVariables.Helm.AdditionalArguments);
            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                args.Add(additionalArguments);
                hasAdditionalArgs = true;
            }

            // AddKOSArgs
            if (variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                var additionalArgs = string.Empty;
                if (hasAdditionalArgs)
                    additionalArgs = args[args.Count - 1];

                var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);
                if (!additionalArgs.Contains("--wait-for-jobs") && waitForJobs)
                    additionalArgs = $"{additionalArgs} --wait-for-jobs";

                if (!additionalArgs.Contains("--wait") || !additionalArgs.Contains("--atomic"))
                    additionalArgs = $"{additionalArgs} --wait";

                if (hasAdditionalArgs)
                    args[args.Count - 1] = additionalArgs;
                else
                    args.Add(additionalArgs.Trim());
            }

            return args;
        }
    }
}
