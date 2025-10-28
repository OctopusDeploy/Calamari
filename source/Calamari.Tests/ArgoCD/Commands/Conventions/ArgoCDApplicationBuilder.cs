using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Domain;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    class ArgoCDApplicationBuilder
    {
        string name = "My App";
        Dictionary<string, string> annotations = new Dictionary<string, string>();
        readonly List<SourceBase> applicationSources = new List<SourceBase>();

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

        public ArgoCDApplicationBuilder WithSource<T>(T source) where T : SourceBase
        {
            applicationSources.Add(source);
            return this;
        }

        public ArgoCDApplicationBuilder WithSources<T>(IEnumerable<T> sources) where T : SourceBase
        {
            applicationSources.AddRange(sources);
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
                }
            };
        }
    }
}