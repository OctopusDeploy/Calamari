using System;
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationParametersFile : ITemplate, ITemplateInputs<Parameter>
    {
        private readonly Func<string> content;
        private readonly Func<string, List<Parameter>> parse;

        public static CloudFormationParametersFile Create(ResolvedTemplatePath path, ICalamariFileSystem fileSystem, CalamariVariableDictionary variables)
        {
            return new CloudFormationParametersFile(() => variables.Evaluate(fileSystem.ReadFile(path.Value)), JsonConvert.DeserializeObject<List<Parameter>>);
        }

        public CloudFormationParametersFile(Func<string> content, Func<string, List<Parameter>> parse)
        {
            this.content = content;
            this.parse = parse;
        }

        public string Content => content();
        public IEnumerable<Parameter> Inputs => content().Map(parse);
    }
}