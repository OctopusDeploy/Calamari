using System;
using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class DeploymentScopeTests
    {
        const string ProjectSlugValue = "the-project";
        const string EnvironmentSlugValue = "the-environment";
        const string StepSlugValue = "update-image-tags";

        [Test]
        public void SourceWithNoStepAnnotation_MatchesAnyStep()
        {
            var annotated = AnnotationsFor(step: null);

            Scope(step: StepSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForThisStep_Matches()
        {
            var annotated = AnnotationsFor(step: StepSlugValue);

            Scope(step: StepSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForAnotherStep_DoesNotMatch()
        {
            var annotated = AnnotationsFor(step: "template-manifests");

            Scope(step: StepSlugValue).Matches(annotated).Should().BeFalse();
        }

        [Test]
        public void StepAnnotationIsTrimmed()
        {
            var annotated = AnnotationsFor(step: "  update-image-tags  ");

            annotated.Step!.Value.Should().Be(StepSlugValue);
        }

        [Test]
        public void StepAnnotationMatchesCaseInsensitively()
        {
            var annotated = AnnotationsFor(step: "Update-Image-Tags");

            Scope(step: StepSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForThisStepButAnotherProject_DoesNotMatch()
        {
            var annotated = ScopingAnnotationReader.GetScopeForApplicationSource(
                null,
                new Dictionary<string, string>
                {
                    [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = "another-project",
                    [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
                    [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = StepSlugValue,
                },
                false);

            Scope(step: StepSlugValue).Matches(annotated).Should().BeFalse();
        }

        [Test]
        public void MultiSourceApplication_UnnamedStepAnnotationIsIgnored()
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = "template-manifests",
            };

            var annotated = ScopingAnnotationReader.GetScopeForApplicationSource(null, annotations, true);

            annotated.Step.Should().BeNull();
        }

        [Test]
        public void NamedSource_StepAnnotationIsReadPerSource()
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("chart"))] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("chart"))] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(new ApplicationSourceName("chart"))] = StepSlugValue,
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("values"))] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("values"))] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(new ApplicationSourceName("values"))] = "template-manifests",
            };

            var scope = Scope(step: StepSlugValue);

            scope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(new ApplicationSourceName("chart"), annotations, true)).Should().BeTrue();
            scope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(new ApplicationSourceName("values"), annotations, true)).Should().BeFalse();
        }

        static DeploymentScope Scope(string step)
            => new(ProjectSlugValue.ToProjectSlug()!, EnvironmentSlugValue.ToEnvironmentSlug()!, null, step.ToStepSlug()!);

        static AnnotationScope AnnotationsFor(string step)
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
            };

            if (step != null)
            {
                annotations[ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = step;
            }

            return ScopingAnnotationReader.GetScopeForApplicationSource(null, annotations, false);
        }
    }
}
