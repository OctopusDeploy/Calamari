using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Inputs.Ecs;
using Calamari.Common.Util;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs;

public record GeneratedTemplate(string Body, IReadOnlyList<Parameter> Parameters);

public class ListTemplateInputs<TInput>(IEnumerable<TInput> inputs) : ITemplateInputs<TInput>
{
    public IEnumerable<TInput> Inputs { get; } = inputs.ToList();
}

public class EcsDeployTemplateGenerator(DeployEcsCommandInputs commandInputs)
{
    readonly App app = new();
    readonly IStackProps stackProps = new StackProps
    {
        Synthesizer = new DefaultStackSynthesizer(new DefaultStackSynthesizerProps
        {
            // This flag kills the Rules assertion section and the bootstrap version parameter completely
            GenerateBootstrapVersionRule = false
        })
    };

    public GeneratedTemplate Generate()
    {
        var parameters = BuildParameters();

        _ = new EcsDeployTemplate(commandInputs, parameters, app, commandInputs.CfStackName, stackProps);

        var assembly = app.Synth();
        var stackArtifact = assembly.GetStackByName(commandInputs.CfStackName);

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        settings.Converters.Add(new WholeDoubleConverter());

        var body = JsonConvert.SerializeObject(stackArtifact.Template, settings);

        return new GeneratedTemplate(
            body,
            parameters.Select(p => new Parameter { ParameterKey = p.Name, ParameterValue = p.Value }).ToList());
    }

    List<(string Name, string Value)> BuildParameters()
    {
        var list = new List<(string Name, string Value)>
        {
            (EcsTemplateParameterNames.ClusterName,          commandInputs.ClusterName),
            (EcsTemplateParameterNames.TaskDefinitionName,   commandInputs.ServiceTaskName),
            (EcsTemplateParameterNames.TaskDefinitionCpu,    commandInputs.Cpu),
            (EcsTemplateParameterNames.TaskDefinitionMemory, commandInputs.Memory),
            (EcsTemplateParameterNames.TaskRole,             commandInputs.TaskRole),
        };

        // Only declared when the user supplied a concrete ARN — otherwise the role
        // is created in-template and referenced via Ref (no parameter needed).
        if (!string.IsNullOrEmpty(commandInputs.TaskExecutionRole))
        {
            list.Add((EcsTemplateParameterNames.TaskExecutionRole, commandInputs.TaskExecutionRole));
        }


        if (commandInputs.RequiresLogGroup)
        {
            list.Add((EcsTemplateParameterNames.LogGroupName, commandInputs.DefaultLogGroupPath));
        }
            

        return list;
    }

    class WholeDoubleConverter : JsonConverter<double?>
    {
        public override void WriteJson(JsonWriter writer, double? value, JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else if (Math.Abs(value.Value - Math.Floor(value.Value)) < double.Epsilon)
                writer.WriteValue((long)value.Value);
            else
                writer.WriteValue(value.Value);
        }

        public override double? ReadJson(JsonReader reader,
                                         Type objectType,
                                         double? existingValue,
                                         bool hasExistingValue,
                                         JsonSerializer serializer)
        {
            return reader.Value == null ? null : Convert.ToDouble(reader.Value);
        }
    }
}
