#if NET
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Helm
{
    public class HelmValuesFileUpdateTargetParserTests
    {
        const string HelmPath1 = "{{ .Values.image.name}}:{{ .Values.image.version}}";
        const string HelmPath2 = "{{ .Values.another-image.name }}";
        const string HelmPath3 = "{{ .Values.another-image.version }}";
        readonly string DoubleItemPathAnnotationValue = $"{HelmPath1}, {HelmPath2}";

        [Test]
        public void GetValuesFilesToUpdate_WithNoSources_ReturnsEmptyList()
        {
            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "Foo"
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, "defaultRegistry");

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithDirectoryOnlySources_ReturnsEmptyList()
        {
            var basicSource = new BasicSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "Foo",
                    Annotations = new Dictionary<string, string>()
                    {
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { basicSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleInlineValuesFile_WithNoAnnotations_ReturnsEmptyList()
        {
            var helmSource = new HelmSource()
            {
                Path = "./",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { "valuesFile.yaml" }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "Foo",
                    Annotations = new Dictionary<string, string>()
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl, null, null) });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleInlineValuesFile_WithDefaultPathAnnotation_ReturnsSource()
        {
            const string valuesFileName = "values.yaml";
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null), DoubleItemPathAnnotationValue }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                     helmSource.Name.ToApplicationSourceName(),
                                                                     ArgoCDConstants.DefaultContainerRegistry,
                                                                     ArgoCDConstants.RefSourcePath,
                                                                     helmSource.RepoUrl,
                                                                     helmSource.TargetRevision,
                                                                     valuesFileName,
                                                                     new List<string>() { HelmPath1, HelmPath2 }
                                                                    );

            result.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expectedSource }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithAliasedInlineValuesFile_WithNoAnnotations_ReturnsInvalidTarget()
        {
            const string valuesFileName = "values.yaml";
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        //{ $"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.Alias1", valuesFileName }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl, null, null) });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithNoAnnotations_ReturnsEmptyList()
        {
            const string valuesFileName1 = "values1.yaml";
            const string valuesFileName2 = "values2.yaml";
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName1, valuesFileName2 }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new []
            {
                new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl, null, null)
            });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithAnnotation_ReturnsSourcesForBothFiles()
        {
            const string valuesFileName1 = "values1.yaml";
            const string valuesFileName2 = "values2.yaml";
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName1, valuesFileName2 }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(null)}", DoubleItemPathAnnotationValue },
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                helmSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                helmSource.Path,
                                                                helmSource.RepoUrl,
                                                                helmSource.TargetRevision,
                                                                valuesFileName2,
                                                                new List<string> { HelmPath1, HelmPath2 }
                                                               );

            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                helmSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                helmSource.Path,
                                                                helmSource.RepoUrl,
                                                                helmSource.TargetRevision,
                                                                valuesFileName1,
                                                                new List<string>() { HelmPath1, HelmPath2 }
                                                               );

            result.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expected1, expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }
        
        [Test]
        public void GetValuesFilesToUpdate_WithRefValuesFiles_WithNoAnnotations_ReturnsEmptyList()
        {
            const string valuesRef = "the-values";
            const string valuesFilePath1 = "files1/values.yaml";
            const string valuesFilePath2 = "files2/values.yaml";

            var refSource = new ReferenceSource()
            {
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Ref = valuesRef,
            };

            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesFilePath1}", $"${valuesRef}/{valuesFilePath2}" }
                }
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl, refSource.Name.ToApplicationSourceName(), refSource.RepoUrl)});
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleRefValuesFile_WithMatchingAnnotations_WithNoAlias_ReturnsSourceForFileInRef()
        {
            const string valuesRef = "the-values";
            const string valuesFilePath = "files/values.yaml";

            var refSource = new ReferenceSource()
            {
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Ref = valuesRef,
            };

            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesFilePath}" }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                     refSource.Name.ToApplicationSourceName(),
                                                                     ArgoCDConstants.DefaultContainerRegistry,
                                                                     ArgoCDConstants.RefSourcePath,
                                                                     refSource.RepoUrl,
                                                                     refSource.TargetRevision,
                                                                     valuesFilePath,
                                                                     new List<string>() { HelmPath1, HelmPath2 }
                                                                    );

            result.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expectedSource }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }
        
        [Test]
        public void GetValuesFilesToUpdate_WithSingleRefValuesFile_AgainstNonExistentRef_Throws()
        {
            // NOTE: This is testing against scenario that should never happen
            // because The Argo CD deployment would fail and thus this app would be unlikely to be pulled into Octopus
            const string valuesRef = "the-values";
            const string valuesFilePath = "files/values.yaml";
            var refSource = new ReferenceSource()
            {
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Ref = "not-here",
            };

            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesFilePath}" }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new RefSourceIsMissing(valuesRef, helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl) });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleRefValuesFiles_WithMatchingAnnotations_WithNoAlias_ReturnsSourcesForFilesInRefs()
        {
            // Same file name/path across 2 different refs
            const string valuesRef1 = "the-values";
            const string valuesRef2 = "other-values";
            const string valuesRepo1Address = "https://github.com/main-repo";
            const string valuesRepo2Address = "https://github.com/another-repo";

            const string valuesFilePath = "files/values.yaml";
            var refSource1 = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepo1Address),
                TargetRevision = "main",
                Ref = valuesRef1,
            };
            var refSource2 = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepo2Address),
                TargetRevision = "main",
                Ref = valuesRef2,
            };
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef1}/{valuesFilePath}", $"${valuesRef2}/{valuesFilePath}" }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue },
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource1, refSource2, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource2.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource2.RepoUrl,
                                                                refSource2.TargetRevision,
                                                                valuesFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 }
                                                               );

            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource1.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource1.RepoUrl,
                                                                refSource1.TargetRevision,
                                                                valuesFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 }
                                                               );

            result.Targets.Should().BeEquivalentTo(new [] { expected2, expected1 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleRefValuesFiles_WithMatchingAnnotations_WithAliasesToRefs_ReturnsSourcesForFilesInRefs()
        {
            //const string alias1 = "values-alias1";
            const string valuesRef1 = "the-values";
            const string valuesRef2 = "other-values";
            const string valuesRepo1Address = "https://github.com/main-repo";
            const string valuesRepo2Address = "https://github.com/another-repo";
            const string valuesFile1Path = "values.yaml";
            const string valuesFile2Path = "config/values.yaml";

            var refSource1 = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepo1Address),
                TargetRevision = "main",
                Ref = valuesRef1,
            };
            var refSource2 = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepo2Address),
                TargetRevision = "main",
                Ref = valuesRef2,
            };
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef1}/{valuesFile1Path}", $"${valuesRef2}/{valuesFile2Path}" }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue },
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource1, refSource2, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource1.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource1.RepoUrl,
                                                                refSource1.TargetRevision,
                                                                valuesFile1Path,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource2.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource2.RepoUrl,
                                                                refSource2.TargetRevision,
                                                                valuesFile2Path,
                                                                new List<string>() { HelmPath1, HelmPath2 });
            ;

            result.Targets.Should().BeEquivalentTo(new [] { expected1, expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMixOfInlineAndRef_WithMatchingAnnotations_ReturnsCorrectSources()
        {
            //const string alias1 = "core";
            const string valuesRef = "remote-values";
            const string valuesRefFilePath = "values.yaml";
            const string inlineValuesFilePath = "app-files/values.yaml";
            const string valuesRepoAddress = "https://github.com/another-repo/values-files-here";
            var refSource = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepoAddress),
                TargetRevision = "main",
                Ref = valuesRef,
            };
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesRefFilePath}", inlineValuesFilePath }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue },
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                helmSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                helmSource.Path,
                                                                helmSource.RepoUrl,
                                                                helmSource.TargetRevision,
                                                                inlineValuesFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource.RepoUrl,
                                                                refSource.TargetRevision,
                                                                valuesRefFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            result.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expected2, expected1 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithTwoFilesFromTheSameRefSeparatedByPaths_WithMatchingAnnotations_ReturnsSourcesForEachPath()
        {
            const string valuesRef = "remote-values";
            const string valuesRefFile1 = "one-path/values.yaml";
            const string valuesRefFile2 = "another-path/values.yaml";
            const string valuesRepoAddress = "https://github.com/another-repo/values-files-here";

            var refSource = new ReferenceSource()
            {
                RepoUrl = new Uri(valuesRepoAddress),
                TargetRevision = "main",
                Ref = valuesRef,
            };
            var helmSource = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesRefFile1}", $"${valuesRef}/{valuesRefFile2}" }
                },
                Name = "chart-source"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue },
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { refSource, helmSource },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource.RepoUrl,
                                                                refSource.TargetRevision,
                                                                valuesRefFile1,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource.RepoUrl,
                                                                refSource.TargetRevision,
                                                                valuesRefFile2,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            result.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expected1, expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleHelmSources_WithInlineValuesFiles_ReturnsValueSourcesForEachHelmSource()
        {
            // App 1
            const string valuesFile1 = "values.yaml";
            const string helmSource1Repo = "https://github.com/my-repo/my-argo-app";
            const string helmSource1Revision = "prod";

            //App 2
            const string valuesFile2 = "app2/values.yaml";
            const string helmSource2Repo = "https://github.com/my-repo/my-other-argo-app";
            const string helmSource2Revision = "main";

            var helmSource1 = new HelmSource()
            {
                Path = "./",
                RepoUrl = new Uri(helmSource1Repo),
                TargetRevision = helmSource1Revision,
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFile1 }
                },
                Name = "helm-1"
            };

            var helmSource2 = new HelmSource()
            {
                Path = "cool",
                RepoUrl = new Uri(helmSource2Repo),
                TargetRevision = helmSource2Revision,
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFile2 }
                },
                Name = "helm-2"
            };

            var toUpdate = new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "TheApp",
                    Annotations = new Dictionary<string, string>()
                    {
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource1.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue },
                        { $"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(helmSource2.Name.ToApplicationSourceName())}", DoubleItemPathAnnotationValue }
                    }
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<SourceBase>() { helmSource1, helmSource2 },
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetValuesFilesToUpdate();

            // Assert
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                helmSource1.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                helmSource1.RepoUrl,
                                                                helmSource1.TargetRevision,
                                                                valuesFile1,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                helmSource2.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                helmSource2.Path,
                                                                helmSource2.RepoUrl,
                                                                helmSource2.TargetRevision,
                                                                valuesFile2,
                                                                new List<string>() { HelmPath1, HelmPath2 });

            result.Targets.Should().BeEquivalentTo(new []{ expected1, expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result.Problems.Should().BeEmpty();
        }
    }
}

#endif
