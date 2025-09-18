using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Kubernetes.ArgoCD.Helm
{
    public class HelmSourceExtensionMethodsTests
    {
        [Test]
        public void GenerateInlineValuesAbsolutePath_WithRootPath_OutputsValidPath()
        {
            const string repoUrl = "https://github.com/helm-test/test";
            const string revision = "main";
            const string valuesFile = "config/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "./",
                Helm = new HelmConfig
                {
                    ValueFiles = new List<string> { valuesFile }
                }
            };

            // Act
            var result = helmSource.GenerateInlineValuesAbsolutePath(valuesFile);

            // Assert
            result.Should().Be("https://github.com/helm-test/test/main/config/values.yaml");
        }

        [Test]
        public void GenerateInlineValuesAbsolutePath_WithDefinedPath_OutputsValidPath()
        {
            const string repoUrl = "https://github.com/helm-test/test";
            const string revision = "main";
            const string valuesFile = "config/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "cool",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFile }
                }
            };

            // Act
            var result = helmSource.GenerateInlineValuesAbsolutePath(valuesFile);

            // Assert
            result.Should().Be("https://github.com/helm-test/test/main/cool/config/values.yaml");
        }

        [Test]
        public void GenerateInlineValuesAbsolutePath_WithExcessSlashCharacters_OutputsValidPath()
        {
            const string repoUrl = "https://github.com/helm-test/slash-app/";
            const string revision = "/dev/";
            const string valuesFile = "/config/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "/slash/",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFile }
                }
            };

            // Act
            var result = helmSource.GenerateInlineValuesAbsolutePath(valuesFile);

            // Assert
            result.Should().Be("https://github.com/helm-test/slash-app/dev/slash/config/values.yaml");
        }

        [Test]
        public void GenerateValuesFilePaths_WithSingleInlineFile_ReturnsQualifiedPath()
        {
            const string repoUrl = "https://github.com/helm-test/test";
            const string revision = "main";
            const string valuesFile = "config/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "cool",
                Helm = new HelmConfig
                {
                    ValueFiles = new List<string> { valuesFile }
                }
            };

            // Act
            var result = helmSource.GenerateValuesFilePaths().ToList();

            result.Count.Should().Be(1);
            result.Should().Contain("https://github.com/helm-test/test/main/cool/config/values.yaml");
        }

        [Test]
        public void GenerateValuesFilePaths_WithMultipleInlineFile_ReturnsQualifiedPathsForEachFile()
        {
            const string repoUrl = "https://github.com/helm-test/test";
            const string revision = "main";
            const string valuesFile = "config/values.yaml";
            const string valuesFile2 = "values/dir/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "cool",
                Helm = new HelmConfig
                {
                    ValueFiles = new List<string> { valuesFile, valuesFile2 }
                }
            };

            // Act
            var result = helmSource.GenerateValuesFilePaths().ToList();

            result.Count.Should().Be(2);
            result.Should().Contain("https://github.com/helm-test/test/main/cool/config/values.yaml");
            result.Should().Contain("https://github.com/helm-test/test/main/cool/values/dir/values.yaml");
        }

        [Test]
        public void GenerateValuesFilePaths_WithRefSources_IncludesRefFilePath()
        {
            const string repoUrl = "https://github.com/helm-test/test";
            const string revision = "main";
            const string valuesFile = "config/values.yaml";
            const string valuesFile2 = "values/dir/values.yaml";
            const string refValuesFile = "$ref-name/values.yaml";

            var helmSource = new HelmSource()
            {
                RepoUrl = new Uri(repoUrl),
                TargetRevision = revision,
                Path = "cool",
                Helm = new HelmConfig
                {
                    ValueFiles = new List<string> { valuesFile, valuesFile2, refValuesFile }
                }
            };

            // Act
            var result = helmSource.GenerateValuesFilePaths().ToList();

            result.Count.Should().Be(3);
            result.Should().Contain("https://github.com/helm-test/test/main/cool/config/values.yaml");
            result.Should().Contain("https://github.com/helm-test/test/main/cool/values/dir/values.yaml");
            result.Should().Contain(refValuesFile);
        }
    }
}