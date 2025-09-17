#if NET
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions.UpdateArgoCdAppImages.Helm;


public class HelmValuesFileUpdateTargetParserTests
{

    const string HelmPath1 = "{{ .Values.image.name}}:{{ .Values.image.version}}";
    const string HelmPath2 = "{{ .Values.another-image.name }}";
    const string HelmPath3 = "{{ .Values.another-image.version }}";
    const string DoubleItemPathAnnotationValue = $"{HelmPath1}, {HelmPath2}";

    public HelmValuesFileUpdateTargetParserTests()
    {
    }

    [Test]
    public void GetValuesFilesToUpdate_WithNoSources_ReturnsEmptyList()
    {
        var toUpdate = new Application()
        {
            
        };

        var sut = new HelmValuesFileUpdateTargetParser(toUpdate, "defaultRegistry");

        // Act
        var result = sut.GetValuesFilesToUpdate();

        // Assert
        result.Should().BeEmpty();
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
        result.Should().BeEmpty();
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
        result.Should().BeEmpty();
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
                    {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, DoubleItemPathAnnotationValue}
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
        var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            helmSource.RepoUrl,
            helmSource.TargetRevision,
            valuesFileName,
            new List<string>() {HelmPath1, HelmPath2}
        );

        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() {expectedSource}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }

    [Test]
    public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithUnaliasedPathAnnotation_Throws()
    {
        const string valuesFileName1 = "values1.yaml";
        const string valuesFileName2 = "values2.yaml";
        var helmSource = new HelmSource()
        {
            Path = "path",
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
                    {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey, DoubleItemPathAnnotationValue}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { helmSource },
            }
        };

        var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

        // Act
        var action = () => sut.GetValuesFilesToUpdate();

        var expectedPaths = $"{helmSource.RepoUrl}/{helmSource.TargetRevision}/{helmSource.Path}/{valuesFileName1}, {helmSource.RepoUrl}/{helmSource.TargetRevision}/{helmSource.Path}/{valuesFileName2}";

        // Assert
        action.Should()
            .Throw<InvalidHelmImageReplaceAnnotationsException>()
            .WithMessage($"Cannot use {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey} without an alias when multiple inline values files are present.\n Values Files: {expectedPaths}");
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
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.Alias1", valuesFileName}
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
        result.Count.Should().Be(1);
        result[0].Should().BeOfType<InvalidHelmValuesFileImageUpdateTarget>();
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
        result.Should().BeEmpty();
    }

    [Test]
    public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithAliasFor1ValuesFile_ReturnsSourceForAliasedFile()
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
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.Alias1", valuesFileName2},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.Alias1", DoubleItemPathAnnotationValue}
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
        var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            helmSource.RepoUrl,
            helmSource.TargetRevision,
            valuesFileName2,
            new List<string> {HelmPath1, HelmPath2}
        );

        result.Should().HaveCount(1);
        result[0].Should().BeEquivalentTo(expectedSource, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }

    [Test]
    public void GetValuesFilesToUpdate_WithMultipleInlineValuesFiles_WithAliasForBothFile_ReturnsSourcesForBothFiles()
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
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.Alias1", valuesFileName2},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.Alias1",  DoubleItemPathAnnotationValue},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.Alias2",  valuesFileName1},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.Alias2",  HelmPath3}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { helmSource },
            }
        };

        var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);

        // Act
        var result = sut.GetValuesFilesToUpdate()
            .OrderBy(s => s.FileName)
            .ToList(); // Add ordering so we can assert properly.

        // Assert
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            helmSource.Path,
            helmSource.RepoUrl,
            helmSource.TargetRevision,
            valuesFileName2,
            new List<string> {HelmPath1, HelmPath2}
        );

        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            helmSource.Path,
            helmSource.RepoUrl,
            helmSource.TargetRevision,
            valuesFileName1,
            new List<string>(){HelmPath3}
        );

        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() {expected1, expected2}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithSingleRefValuesFile_WithNoAnnotations_ReturnsEmptyList()
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
        result.Should().BeEmpty();
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
            }
        };
        
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef}", DoubleItemPathAnnotationValue}
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
        var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource.RepoUrl,
            refSource.TargetRevision,
            valuesFilePath,
            new List<string>() {HelmPath1, HelmPath2}
        );
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>(){expectedSource}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithSingleRefValuesFile_WithMatchingAnnotations_WithAliasToRefFile_ReturnsSourceForFileInRef()
    {
        const string alias = "Alias1";
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
            }
        };
        
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias}",  $"${valuesRef}/{valuesFilePath}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias}", DoubleItemPathAnnotationValue}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { refSource, helmSource },
            }
        };
    
        var sut = new HelmValuesFileUpdateTargetParser(toUpdate,  ArgoCDConstants.DefaultContainerRegistry);
    
        // Act
        var result = sut.GetValuesFilesToUpdate();
    
        // Assert
        var expectedSource = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource.RepoUrl,
            refSource.TargetRevision,
            valuesFilePath,
            new List<string>(){HelmPath1, HelmPath2}
        );
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() {expectedSource}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithSingleRefValuesFile_WithAliasToRefFile_ButAnnotationsDirectlyAgainstRef_ReturnsInvalidSource()
    {
        const string alias = "Alias1";
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
            }
        };
    
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias}",  $"${valuesRef}/{valuesFilePath}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{refSource}", DoubleItemPathAnnotationValue}
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
        result.Count.Should().Be(1);
        result[0].Should().BeOfType<InvalidHelmValuesFileImageUpdateTarget>();
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
            }
        };
        
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef}", DoubleItemPathAnnotationValue}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { refSource, helmSource },
            }
        };
    
        var sut = new HelmValuesFileUpdateTargetParser(toUpdate,  ArgoCDConstants.DefaultContainerRegistry);
    
        // Act
        var act = () => sut.GetValuesFilesToUpdate();
    
        // Assert
        act.Should().Throw<InvalidHelmImageReplaceAnnotationsException>();
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
            }
        };
    
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef1}", DoubleItemPathAnnotationValue},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef2}", HelmPath3}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { refSource1, refSource2, helmSource },
            }
        };
    
        var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);
    
        // Act
        var result = sut.GetValuesFilesToUpdate()
            .OrderBy(s => s.RepoUrl.ToString())
            .ToHashSet();
    
        // Assert
        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource2.RepoUrl,
            refSource2.TargetRevision,
            valuesFilePath,
            new List<string>() {HelmPath3}
        );
    
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource1.RepoUrl,
            refSource1.TargetRevision,
            valuesFilePath,
            new List<string>() {HelmPath1, HelmPath2}
        );
    
        result.Should().BeEquivalentTo(new HashSet<HelmValuesFileImageUpdateTarget>() {expected2, expected1}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithMultipleRefValuesFiles_WithMatchingAnnotations_WithAliasesToRefs_ReturnsSourcesForFilesInRefs()
    {
        const string alias1 = "values-alias1";
        const string alias2 = "other-alias";
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
            }
        };
    
        var annotations = new Dictionary<string, string>
        {
            [$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}"] = $"${valuesRef1}/{valuesFile1Path}",
            [$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}"] = DoubleItemPathAnnotationValue,
            [$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias2}"] = $"${valuesRef2}/{valuesFile2Path}",
            [$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias2}"] = HelmPath3
        };
    
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}",  $"${valuesRef1}/{valuesFile1Path}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}", DoubleItemPathAnnotationValue},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias2}", $"${valuesRef2}/{valuesFile2Path}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias2}", HelmPath3}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { refSource1, refSource2, helmSource },
            }
        };
    
        var sut = new HelmValuesFileUpdateTargetParser(toUpdate, ArgoCDConstants.DefaultContainerRegistry);
    
        // Act
        var result = sut.GetValuesFilesToUpdate()
            .OrderBy(s => s.RepoUrl.ToString())
            .ToList(); // order for assert
    
        // Assert
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource1.RepoUrl,
            refSource1.TargetRevision,
            valuesFile1Path,
            new List<string>() {HelmPath1, HelmPath2});

        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
                                                            ArgoCDConstants.DefaultContainerRegistry,
                                                            ArgoCDConstants.RefSourcePath,
                                                            refSource2.RepoUrl,
                                                            refSource1.TargetRevision,
                                                            valuesFile2Path,
                                                            new List<string>() { HelmPath3 });;
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>() {expected1, expected2}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithMixOfInlineAndRef_WithMatchingAnnotations_ReturnsCorrectSources()
    {
        const string alias1 = "core";
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
                ValueFiles = new List<string>() { $"${valuesRef}/{valuesRefFilePath}",inlineValuesFilePath }
            }
        };
    
        var annotations = new Dictionary<string, string>
        {
            [$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}"] = inlineValuesFilePath,
            [$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}"] = DoubleItemPathAnnotationValue,
            [$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef}"] = HelmPath3
        };
    
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}",  inlineValuesFilePath},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}", DoubleItemPathAnnotationValue},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{valuesRef}", HelmPath3}
                }
            },
            Spec = new ApplicationSpec()
            {
                Sources = new List<SourceBase>() { refSource, helmSource },
            }
        };
    
        var sut = new HelmValuesFileUpdateTargetParser(toUpdate,  ArgoCDConstants.DefaultContainerRegistry);
    
        // Act
        var result = sut.GetValuesFilesToUpdate();
    
        // Assert
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            helmSource.Path,
            helmSource.RepoUrl,
            helmSource.TargetRevision,
            inlineValuesFilePath,
            new List<string>(){HelmPath1, HelmPath2});
    
        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource.RepoUrl,
            refSource.TargetRevision,
            valuesRefFilePath,
            new List<string>(){HelmPath3});
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>(){expected2, expected1}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
    [Test]
    public void GetValuesFilesToUpdate_WithTwoFilesFromTheSameRefSeparatedByPaths_WithMatchingAnnotations_ReturnsSourcesForEachPath()
    {
        const string alias1 = "core";
        const string alias2 = "overlay";
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
            }
        };
        
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}", $"${valuesRef}/{valuesRefFile1}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}", DoubleItemPathAnnotationValue},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias2}", $"${valuesRef}/{valuesRefFile2}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias2}", HelmPath3},
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
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource.RepoUrl,
            refSource.TargetRevision,
            valuesRefFile1,
            new List<string>(){HelmPath1, HelmPath2});
    
        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            refSource.RepoUrl,
            refSource.TargetRevision,
            valuesRefFile2,
            new List<string>(){HelmPath3});
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>(){expected1, expected2}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
    
     [Test]
     public void GetValuesFilesToUpdate_WithTwoFilesFromTheSameRefSeparatedByPaths_OnlyOneAliasedAnnotation_IgnoresUnaliasedValuesFile()
     {
         const string alias1 = "core";
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
             }
         };
         
        var toUpdate = new Application()
         {
             Metadata = new Metadata()
             {
                 Name = "TheApp",
                 Annotations = new Dictionary<string, string>()
                 {
                     {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}", $"${valuesRef}/{valuesRefFile1}"},
                     {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}", DoubleItemPathAnnotationValue},
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
    
         var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
             ArgoCDConstants.DefaultContainerRegistry,
             ArgoCDConstants.RefSourcePath,
             refSource.RepoUrl,
             refSource.TargetRevision,
             valuesRefFile1,
             new List<string>(){HelmPath1, HelmPath2});
         result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>(){expected1}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
     }
    
    [Test]
    public void GetValuesFilesToUpdate_WithMultipleHelmSources_WithInlineValuesFiles_ReturnsValueSourcesForEachHelmSource()
    {
        // App 1
        const string alias1 = "app1";
        const string valuesFile1 = "values.yaml";
        const string helmSource1Repo = "https://github.com/my-repo/my-argo-app";
        const string helmSource1Revision = "prod";
    
        //App 2
        const string alias2 = "app2";
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
            }
        };

        var helmSource2 = new HelmSource()
        {
            Path = "cool",
            RepoUrl = new Uri(helmSource2Repo),
            TargetRevision = helmSource2Revision,
            Helm = new HelmConfig()
            {
                ValueFiles = new List<string>() { valuesFile2 }
            }
        };
    
        var toUpdate = new Application()
        {
            Metadata = new Metadata()
            {
                Name = "TheApp",
                Annotations = new Dictionary<string, string>()
                {
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias1}", $"{helmSource1Repo}/{helmSource1Revision}/{valuesFile1}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias1}", DoubleItemPathAnnotationValue},
    
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplaceAliasKey}.{alias2}", $"{helmSource2Repo}/{helmSource2Revision}/cool/{valuesFile2}"},
                    {$"{ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey}.{alias2}", DoubleItemPathAnnotationValue}
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
        result.Count.Should().Be(2);
    
        var expected1 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            ArgoCDConstants.RefSourcePath,
            helmSource1.RepoUrl,
            helmSource1.TargetRevision,
            valuesFile1,
            new List<string>(){HelmPath1, HelmPath2});
    
        var expected2 = new HelmValuesFileImageUpdateTarget(toUpdate.Metadata.Name,
            ArgoCDConstants.DefaultContainerRegistry,
            helmSource2.Path,
            helmSource2.RepoUrl,
            helmSource2.TargetRevision,
            valuesFile2,
            new List<string>(){HelmPath1, HelmPath2});
    
        result.Should().BeEquivalentTo(new List<HelmValuesFileImageUpdateTarget>(){expected1, expected2}, options => options.ComparingByMembers<HelmValuesFileImageUpdateTarget>());
    }
}

#endif
