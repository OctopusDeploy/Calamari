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

        public ArgoCDApplicationBuilder WithSource(ApplicationSource source, string sourceType)
        {
            applicationSources.Add(source);
            if (sourceType != null)
            {
                applicationSourceTypes.Add(sourceType);
            }
            return this;
        }

        public ArgoCDApplicationBuilder WithSources(IEnumerable<ApplicationSource> sources, IEnumerable<string> sourceTypes)
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