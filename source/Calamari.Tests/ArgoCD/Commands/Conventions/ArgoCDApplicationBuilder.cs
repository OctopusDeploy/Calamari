using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Domain;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    class ArgoCDApplicationBuilder
    {
        string name = "My App";
        Dictionary<string, string> annotations = new Dictionary<string, string>();
        readonly List<ApplicationSource> applicationSources = new List<ApplicationSource>();
        readonly List<string> applicationSourceTypes = new List<string>();

        public ArgoCDApplicationBuilder WithName(string value)
        {
            name = value;
            return this;
        }

        public ArgoCDApplicationBuilder WithAnnotations(Dictionary<string, string> value)
        {
            annotations = value;
            return this;
        }

        public ArgoCDApplicationBuilder WithSource<T>(T source, string sourceType) where T : ApplicationSource
        {
            applicationSources.Add(source);
            if (sourceType != null)
            {
                applicationSourceTypes.Add(sourceType);
            }
            return this;
        }

        public ArgoCDApplicationBuilder WithSources<T>(IEnumerable<T> sources, IEnumerable<string> sourceTypes) where T : ApplicationSource
        {
            applicationSources.AddRange(sources);
            applicationSourceTypes.AddRange(sourceTypes);
            return this;
        }

        public Application Build()
        {
            return new Application()
            {
                Metadata = new Metadata()
                {
                    Name = name,
                    Annotations = annotations
                },
                Spec = new ApplicationSpec()
                {
                    Sources = applicationSources
                },
                Status = new ApplicationStatus()
                {
                    SourceTypes = applicationSourceTypes
                }
            };
        }
    }
}