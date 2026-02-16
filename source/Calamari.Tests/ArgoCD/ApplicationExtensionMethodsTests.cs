using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Tests.ArgoCD.Commands.Conventions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
  [TestFixture]
  public class ApplicationExtensionMethodsTests
  {
    [Test]
    public void GetSourcesWithMetadata_SourceTypeCountMatchesSources_SourceTypeIsSet()
    {
      var names = new[] { "s1", "s2" };
      var application = new ArgoCDApplicationBuilder()
             .WithName("FooApp")
             .WithSources(names.Select(n => new ApplicationSource { Name = n}), new [] {SourceTypeConstants.Helm, SourceTypeConstants.Directory})
             .Build();

      var sources = application.GetSourcesWithMetadata();

      sources.Should().SatisfyRespectively(
                                           s1 => AssertSource(s1, "s1", 0, SourceType.Helm),
                                           s2 => AssertSource(s2, "s2", 1, SourceType.Directory));
    }

    [Test]
    public void GetSourcesWithMetadata_SourceTypesIsEmpty_SourceTypeIsNull()
    {
      var names = new[] { "s1", "s2" };
      var application = new ArgoCDApplicationBuilder()
                        .WithName("FooApp")
                        .WithSources(names.Select(n => new ApplicationSource { Name = n}), new string[] {})
                        .Build();

      var sources = application.GetSourcesWithMetadata();

      sources.Should().SatisfyRespectively(
                                           s1 => AssertSource(s1, "s1", 0, null),
                                           s2 => AssertSource(s2, "s2", 1, null));
    }

    [Test]
    public void GetSourcesWithMetadata_SourceTypesCountIsLess_SourceTypeIsNull()
    {
      var names = new[] { "s1", "s2" };
      var application = new ArgoCDApplicationBuilder()
                        .WithName("FooApp")
                        .WithSources(names.Select(n => new ApplicationSource { Name = n}), new string[] {SourceTypeConstants.Directory})
                        .Build();

      var sources = application.GetSourcesWithMetadata();

      sources.Should().SatisfyRespectively(
                                           s1 => AssertSource(s1, "s1", 0, null),
                                           s2 => AssertSource(s2, "s2", 1, null));
    }
    
    [Test]
    public void GetSourcesWithMetadata_SourceTypesCountIsMore_SourceTypeIsNull()
    {
        var names = new[] { "s1", "s2" };
        var application = new ArgoCDApplicationBuilder()
                          .WithName("FooApp")
                          .WithSources(names.Select(n => new ApplicationSource { Name = n}), new string[] {SourceTypeConstants.Directory, SourceTypeConstants.Kustomize, SourceTypeConstants.Plugin})
                          .Build();

        var sources = application.GetSourcesWithMetadata();

        sources.Should().SatisfyRespectively(
                                             s1 => AssertSource(s1, "s1", 0, null),
                                             s2 => AssertSource(s2, "s2", 1, null));
    }
    
    //Sources without PATH specified (usually the REF sources) end up as empty strings. The Argo UI displays it as `Directory` anyway
    [Test]
    public void GetSourcesWithMetadata_SourceTypeIsEmptyStringOrNull_SourceTypeIsDirectory()
    {
      var names = new[] { "s1", "s2", "s3" };
      var application = new ArgoCDApplicationBuilder()
                        .WithName("FooApp")
                        .WithSources(names.Select(n => new ApplicationSource { Name = n}), new string[] { "", null, SourceTypeConstants.Kustomize})
                        .Build();

      var sources = application.GetSourcesWithMetadata();

      sources.Should().SatisfyRespectively(
                                           s1 => AssertSource(s1, "s1", 0, SourceType.Directory),
                                           s2 => AssertSource(s2, "s2", 1, SourceType.Directory),
                                           s2 => AssertSource(s2, "s3", 2, SourceType.Kustomize));
    }
    
    [Test]
    public void GetSourcesWithMetadata_SourceTypeIsUnrecognized_SourceTypeIsNull()
    {
      var names = new[] { "s1", "s2" };
      var application = new ArgoCDApplicationBuilder()
                        .WithName("FooApp")
                        .WithSources(names.Select(n => new ApplicationSource { Name = n}), new string[] { "Unrecognized", SourceTypeConstants.Kustomize})
                        .Build();

      var sources = application.GetSourcesWithMetadata();

      sources.Should().SatisfyRespectively(
                                           s1 => AssertSource(s1, "s1", 0, null),
                                           s2 => AssertSource(s2, "s2", 1, SourceType.Kustomize));
    }

    static void AssertSource(ApplicationSourceWithMetadata source, string name, int index, SourceType? sourceType)
    {
      source.Source.Name.Should().Be(name);
      source.Index.Should().Be(index);
      source.SourceType.Should().Be(sourceType);
    }
  }
}
