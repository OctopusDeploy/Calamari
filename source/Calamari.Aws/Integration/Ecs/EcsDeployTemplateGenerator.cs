using System;
using Amazon.CDK;
using Calamari.Aws.Inputs;
using Calamari.Aws.Inputs.Ecs;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs;

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
    
    public string GenerateTemplate()
    {
        _ = new EcsDeployTemplate(commandInputs, app, commandInputs.CfStackName, stackProps);

        var assembly = app.Synth();

        var stackArtifact = assembly.GetStackByName(commandInputs.CfStackName);

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