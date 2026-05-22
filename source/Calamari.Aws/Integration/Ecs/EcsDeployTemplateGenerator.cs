using Amazon.CDK;
using Calamari.Aws.Inputs;
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

        return JsonConvert.SerializeObject(stackArtifact.Template, settings);
    }
}