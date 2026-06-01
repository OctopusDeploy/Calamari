using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CDK;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Inputs.Ecs;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Deploy;

public record GeneratedTemplate(string Body, IReadOnlyList<Parameter> Parameters);


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

    List<IEcsTemplateParameter> BuildParameters()
    {
        var list = new List<IEcsTemplateParameter>
        {
            EcsTemplateParameter.Of(EcsTemplateParameterNames.ClusterName,          commandInputs.ClusterName),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionName,   commandInputs.ServiceTaskName),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionCpu,    commandInputs.Cpu),
            EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskDefinitionMemory, commandInputs.Memory),
        };

        // The remaining parameters are only registered when the user-supplied value
        // differs from the default — matching SPF, which keeps the template lean
        // when defaults are in use and parameterises only what's been customised.

        if (!string.IsNullOrEmpty(commandInputs.TaskRole))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskRole, commandInputs.TaskRole));
        }

        // Only declared when the user supplied a concrete ARN — otherwise the role
        // is created in-template and referenced via Ref (no parameter needed).
        if (!string.IsNullOrEmpty(commandInputs.TaskExecutionRole))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.TaskExecutionRole, commandInputs.TaskExecutionRole));
        }

        if (DiffersFromDefault(commandInputs.DesiredCount, EcsInputDefaults.DesiredCount))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.DesiredCount, commandInputs.DesiredCount));
        }

        if (DiffersFromDefault(commandInputs.MinimumHealthyPercentage, EcsInputDefaults.MinimumHealthPercent))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.MinimumHealthPercent, commandInputs.MinimumHealthyPercentage));
        }

        if (DiffersFromDefault(commandInputs.MaximumHealthyPercentage, EcsInputDefaults.MaximumHealthPercent))
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.MaximumHealthPercent, commandInputs.MaximumHealthyPercentage));
        }

        if (commandInputs.RequiresLogGroup)
        {
            list.Add(EcsTemplateParameter.Of(EcsTemplateParameterNames.LogGroupName, commandInputs.DefaultLogGroupPath));
        }

        return list;
    }

    // Direct `!=` on doubles is unreliable across precision and NaN; compare via
    // epsilon-based equality (matches the WholeDoubleConverter convention below).
    static bool DiffersFromDefault(double value, double @default) =>
        Math.Abs(value - @default) > double.Epsilon;

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
