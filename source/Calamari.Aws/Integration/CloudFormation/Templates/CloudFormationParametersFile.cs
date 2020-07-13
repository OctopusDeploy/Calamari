using System;
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;
using Octopus.CoreUtilities;
using Calamari.Common.Util;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationParametersFile : ITemplate, ITemplateInputs<Parameter>
    {
        readonly Func<Maybe<string>> content;
        readonly Func<string, List<Parameter>> parse;

        public static CloudFormationParametersFile Create(Maybe<ResolvedTemplatePath> path, ICalamariFileSystem fileSystem, IVariables variables)
        {
            return new CloudFormationParametersFile(() => path.Select(x => variables.Evaluate(fileSystem.ReadFile(x.Value))), JsonConvert.DeserializeObject<List<Parameter>>);
        }

        public CloudFormationParametersFile(Func<Maybe<string>> content, Func<string, List<Parameter>> parse)
        {
            this.content = content;
            this.parse = parse;
        }

        public string Content => content().SomeOrDefault();
        public IEnumerable<Parameter> Inputs => content().Select(parse).SelectValueOr(x => x, new List<Parameter>());
    }
}