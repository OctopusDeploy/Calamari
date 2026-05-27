using System;
using Amazon.CDK;
using Calamari.Aws.Inputs;
using Calamari.Aws.Inputs.Ecs;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs;

public static class EcsDeployTemplateGenerator
{
    public static string GenerateTemplate(DeployEcsCommandInputs commandInputs)
    {
        var stackName = commandInputs.CfStackName;

        var app = new App();

        var stackProps = new StackProps
        {
            Synthesizer = new DefaultStackSynthesizer(new DefaultStackSynthesizerProps
            {
                // This flag kills the Rules assertion section and the bootstrap version parameter completely
                GenerateBootstrapVersionRule = false
            })
        };

        _ = new EcsDeployTemplate(commandInputs, app, stackName, stackProps);

        var assembly = app.Synth();

        var stackArtifact = assembly.GetStackByName(stackName);

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        
        settings.Converters.Add(new WholeDoubleConverter());

        return JsonConvert.SerializeObject(stackArtifact.Template, settings);
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