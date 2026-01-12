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
        readonly string DoubleItemPathAnnotationValue = $"{HelmPath1}, {HelmPath2}";

        // [Test]
        // public void GetValuesFilesToUpdate_WithNoSources_ReturnsEmptyList()
        // {
        //     var toUpdate = new Application()
        //     {
        //         Metadata = new Metadata()
        //         {
        //             Name = "Foo"
        //         }
        //     };
        //
        //     var sut = new HelmValuesFileUpdateTargetParser(toUpdate, "defaultRegistry");
        //
        //     // Act
        //     var result = sut.GetValuesFilesToUpdate();
        //
        //     // Assert
        //     result.Targets.Should().BeEmpty();
        //     result.Problems.Should().BeEmpty();
        // }

        [Test]
        public void GetValuesFilesToUpdate_WithDirectoryOnlySources_ReturnsEmptyList()
        {
            var basicSource = new ApplicationSource()
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
                },
                Spec = new ApplicationSpec()
                {
                    Sources = new List<ApplicationSource>() { basicSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetExplicitValuesFilesToUpdate(basicSource);

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleInlineValuesFile_WithNoAnnotations_ReturnsEmptyListWithProblem()
        {
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { "valuesFile.yaml" }
                },
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
                    Sources = new List<ApplicationSource>() { helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetExplicitValuesFilesToUpdate(helmSource);

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl) });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleInlineValuesFile_WithDefaultPathAnnotation_ReturnsSource()
        {
            const string valuesFileName = "values.yaml";
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName }
                },
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
                    Sources = new List<ApplicationSource>() { helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetExplicitValuesFilesToUpdate(helmSource);

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
        public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithNoAnnotations_ReturnsEmptyListWithSingleProblem()
        {
            const string valuesFileName1 = "values1.yaml";
            const string valuesFileName2 = "values2.yaml";
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName1, valuesFileName2 }
                },
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
                    Sources = new List<ApplicationSource>() { helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetExplicitValuesFilesToUpdate(helmSource);

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new []
            {
                new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl)
            });
        }

        [Test]
        public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithAnnotation_ReturnsSourcesForBothFiles()
        {
            const string valuesFileName1 = "values1.yaml";
            const string valuesFileName2 = "values2.yaml";
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { valuesFileName1, valuesFileName2 }
                },
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
                    Sources = new List<ApplicationSource>() { helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetExplicitValuesFilesToUpdate(helmSource);

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
        public void GetValuesFilesToUpdate_WithRefValuesFiles_WithNoAnnotations_ReturnsEmptyListWithProblem()
        {
            const string valuesRef = "the-values";
            const string valuesFilePath1 = "files1/values.yaml";
            const string valuesFilePath2 = "files2/values.yaml";

            var refSource = new ApplicationSource()
            {
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Ref = valuesRef,
            };

            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesFilePath1}", $"${valuesRef}/{valuesFilePath2}" }
                },
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
                    Sources = new List<ApplicationSource>() { refSource, helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory, SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetHelmTargetsForRefSource(refSource);

            // Assert
            result.Targets.Should().BeEmpty();
            result.Problems.Should().BeEquivalentTo(new [] { new HelmSourceIsMissingImagePathAnnotation(helmSource.Name.ToApplicationSourceName(), helmSource.RepoUrl, refSource.Name.ToApplicationSourceName())});
        }

        [Test]
        public void GetValuesFilesToUpdate_WithSingleRefValuesFile_WithMatchingAnnotations_ReturnsSourceForFileInRef()
        {
            const string valuesRef = "the-values";
            const string valuesFilePath = "files/values.yaml";

            var refSource = new ApplicationSource()
            {
                RepoUrl = new Uri("https://example.com/repo.git"),
                TargetRevision = "main",
                Ref = valuesRef,
            };

            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesFilePath}" }
                },
                Name = "chart-source",
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
                    Sources = new List<ApplicationSource>() { refSource, helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory, SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetHelmTargetsForRefSource(refSource);

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
        public void GetValuesFilesToUpdate_WithMultipleRefValuesFiles_WithMatchingAnnotations_ReturnsSourcesForFilesInRefs()
        {
            // Same file name/path across 2 different refs
            const string valuesRef1 = "the-values";
            const string valuesRef2 = "other-values";
            const string valuesRepo1Address = "https://github.com/main-repo";
            const string valuesRepo2Address = "https://github.com/another-repo";

            const string valuesFilePath = "files/values.yaml";
            var refSource1 = new ApplicationSource()
            {
                RepoUrl = new Uri(valuesRepo1Address),
                TargetRevision = "main",
                Ref = valuesRef1,
            };
            var refSource2 = new ApplicationSource()
            {
                RepoUrl = new Uri(valuesRepo2Address),
                TargetRevision = "main",
                Ref = valuesRef2,
            };
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef1}/{valuesFilePath}", $"${valuesRef2}/{valuesFilePath}" }
                },
                Name = "chart-source",
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
                    Sources = new List<ApplicationSource>() { refSource1, refSource2, helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory, SourceTypeConstants.Directory, SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result2 = sut.GetHelmTargetsForRefSource(refSource2);
            var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource2.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource2.RepoUrl,
                                                                refSource2.TargetRevision,
                                                                valuesFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 }
                                                               );
            result2.Targets.Should().BeEquivalentTo(new [] { expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result2.Problems.Should().BeEmpty();

            // Assert
        

            var result1 = sut.GetHelmTargetsForRefSource(refSource1);
            var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name.ToApplicationName(),
                                                                refSource1.Name.ToApplicationSourceName(),
                                                                ArgoCDConstants.DefaultContainerRegistry,
                                                                ArgoCDConstants.RefSourcePath,
                                                                refSource1.RepoUrl,
                                                                refSource1.TargetRevision,
                                                                valuesFilePath,
                                                                new List<string>() { HelmPath1, HelmPath2 }
                                                               );

            result1.Targets.Should().BeEquivalentTo(new [] { expected1 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result1.Problems.Should().BeEmpty();
        }
        
        [Test]
        public void GetValuesFilesToUpdate_WithMixOfInlineAndRef_WithMatchingAnnotations_ReturnsCorrectSources()
        {
            //const string alias1 = "core";
            const string valuesRef = "remote-values";
            const string valuesRefFilePath = "values.yaml";
            const string inlineValuesFilePath = "app-files/values.yaml";
            const string valuesRepoAddress = "https://github.com/another-repo/values-files-here";
            var refSource = new ApplicationSource()
            {
                RepoUrl = new Uri(valuesRepoAddress),
                TargetRevision = "main",
                Ref = valuesRef,
            };
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesRefFilePath}", inlineValuesFilePath }
                },
                Name = "chart-source",
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
                    Sources = new List<ApplicationSource>() { refSource, helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory, SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            
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

            var result1 = sut.GetExplicitValuesFilesToUpdate(helmSource);
            result1.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expected1 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result1.Problems.Should().BeEmpty();

            var result2 = sut.GetHelmTargetsForRefSource(refSource);
            result2.Targets.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() { expected2 }, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
            result2.Problems.Should().BeEmpty();
        }

        [Test]
        public void GetValuesFilesToUpdate_WithTwoFilesFromTheSameRefSeparatedByPaths_WithMatchingAnnotations_ReturnsSourcesForEachPath()
        {
            const string valuesRef = "remote-values";
            const string valuesRefFile1 = "one-path/values.yaml";
            const string valuesRefFile2 = "another-path/values.yaml";
            const string valuesRepoAddress = "https://github.com/another-repo/values-files-here";

            var refSource = new ApplicationSource()
            {
                RepoUrl = new Uri(valuesRepoAddress),
                TargetRevision = "main",
                Ref = valuesRef,
            };
            var helmSource = new ApplicationSource()
            {
                Path = "./",
                RepoUrl = new Uri("https://example.com/repo.git"),
                Helm = new HelmConfig()
                {
                    ValueFiles = new List<string>() { $"${valuesRef}/{valuesRefFile1}", $"${valuesRef}/{valuesRefFile2}" }
                },
                Name = "chart-source",
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
                    Sources = new List<ApplicationSource>() { refSource, helmSource },
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = new List<string>(new[] { SourceTypeConstants.Directory, SourceTypeConstants.Helm })
                }
            };

            var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

            // Act
            var result = sut.GetHelmTargetsForRefSource(refSource);

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
    }
}

#endif
