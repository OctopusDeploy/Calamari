using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Amazon.CloudFormation.Model;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class CloudFormationTemplate: ITemplate, ITemplateInputs<Parameter>, ITemplateOutputs<StackFormationNamedOutput>
    {
        private readonly Func<string> content;
        private readonly Func<string, List<StackFormationNamedOutput>> parse;
        private ITemplateInputs<Parameter> parameters;
        private static readonly Regex OutputsRe = new Regex("\"?Outputs\"?\\s*:");
        
        public CloudFormationTemplate(Func<string> content, ITemplateInputs<Parameter> parameters, Func<string, List<StackFormationNamedOutput>> parse)
        {
            this.content = content;
            this.parameters = parameters;
            this.parse = parse;
        }

        public static CloudFormationTemplate Create(ResolvedTemplatePath path, ITemplateInputs<Parameter> parameters, ICalamariFileSystem filesSystem, IVariables variables)
        {
            Guard.NotNull(path, "Path must not be null");
            return new CloudFormationTemplate(() => variables.Evaluate(filesSystem.ReadFile(path.Value)), parameters, JsonConvert.DeserializeObject<List<StackFormationNamedOutput>> );
        }

        public string Content => content();

        public IEnumerable<Parameter> Inputs => parameters.Inputs;
        public bool HasOutputs => Content.Map(OutputsRe.IsMatch);
        public IEnumerable<StackFormationNamedOutput> Outputs  => HasOutputs ? parse(Content) : new List<StackFormationNamedOutput>();
    }
}