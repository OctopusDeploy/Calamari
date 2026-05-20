using Amazon.CDK;
using Calamari.Aws.Inputs;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Calamari.Aws.Integration.Ecs;

public static class EcsDeployTemplateGenerator
{
    public static string GenerateTemplate(DeployEcsCommandInputs commandInputs)
    {
        var stackName = commandInputs.CfStackName;
        
        var app = new App();

        _ = new EcsDeployTemplate(commandInputs, app, stackName);

        var assembly = app.Synth();
        
        var stackArtifact = assembly.GetStackByName(stackName);
        
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented, 
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver() 
        };
        
        var cloudFormationTemplateJson = JsonConvert.SerializeObject(stackArtifact.Template, settings);

        return cloudFormationTemplateJson;
    }
}