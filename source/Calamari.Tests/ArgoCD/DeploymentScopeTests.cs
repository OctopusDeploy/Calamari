using System;
using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class DeploymentScopeTests
    {
        const string ProjectSlugValue = "the-project";
        const string EnvironmentSlugValue = "the-environment";
        const string ActionSlugValue = "update-image-tags";

        [Test]
        public void SourceWithNoStepAnnotation_MatchesAnyAction()
        {
            var annotated = AnnotationsFor(action: null);

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForThisAction_Matches()
        {
            var annotated = AnnotationsFor(action: ActionSlugValue);

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForAnotherAction_DoesNotMatch()
        {
            var annotated = AnnotationsFor(action: "template-manifests");

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeFalse();
        }

        [Test]
        public void StepAnnotationMatchesAfterTrimming()
        {
            var annotated = AnnotationsFor(action: "  update-image-tags  ");

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void StepAnnotationMatchesCaseInsensitively()
        {
            var annotated = AnnotationsFor(action: "Update-Image-Tags");

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeTrue();
        }

        [Test]
        public void SourceAnnotatedForThisActionButAnotherProject_DoesNotMatch()
        {
            var annotated = ScopingAnnotationReader.GetScopeForApplicationSource(
                null,
                new Dictionary<string, string>
                {
                    [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = "another-project",
                    [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
                    [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = ActionSlugValue,
                },
                false);

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeFalse();
        }

        [Test]
        public void MultiSourceApplication_UnnamedAnnotations_DoNotMatchUnnamedSource()
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = ActionSlugValue,
            };

            var annotated = ScopingAnnotationReader.GetScopeForApplicationSource(null, annotations, true);

            Scope(action: ActionSlugValue).Matches(annotated).Should().BeFalse();
        }

        [Test]
        public void NamedSource_StepAnnotationIsReadPerSource()
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("chart"))] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("chart"))] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(new ApplicationSourceName("chart"))] = ActionSlugValue,
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(new ApplicationSourceName("values"))] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(new ApplicationSourceName("values"))] = EnvironmentSlugValue,
                [ArgoCDConstants.Annotations.OctopusStepAnnotationKey(new ApplicationSourceName("values"))] = "template-manifests",
            };

            var scope = Scope(action: ActionSlugValue);

            scope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(new ApplicationSourceName("chart"), annotations, true)).Should().BeTrue();
            scope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(new ApplicationSourceName("values"), annotations, true)).Should().BeFalse();
        }

        [Test]
        public void MissingActionSlug_DoesNotFallBackToParentStepSlug()
        {
            var variables = new CalamariVariables
            {
                [ProjectVariables.Slug] = ProjectSlugValue,
                [DeploymentEnvironment.Slug] = EnvironmentSlugValue,
                [StepVariables.Slug] = "parent-step",
            };

            Action getScope = () => variables.GetDeploymentScope();

            getScope.Should().Throw<CommandException>();
        }

        static DeploymentScope Scope(string action)
            => new(ProjectSlugValue.ToProjectSlug()!, EnvironmentSlugValue.ToEnvironmentSlug()!, null, action.ToActionSlug()!);

        static AnnotationScope AnnotationsFor(string action)
        {
            var annotations = new Dictionary<string, string>
            {
                [ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(null)] = ProjectSlugValue,
                [ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(null)] = EnvironmentSlugValue,
            };

            if (action != null)
            {
                annotations[ArgoCDConstants.Annotations.OctopusStepAnnotationKey(null)] = action;
            }

            return ScopingAnnotationReader.GetScopeForApplicationSource(null, annotations, false);
        }
    }
}
