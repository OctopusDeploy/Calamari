using Amazon.ECS.Model;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Update;

public static class TaskDefinitionEquality
{
    static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    public static bool AreSame(TaskDefinition a, TaskDefinition b) =>
        JsonConvert.SerializeObject(a, Settings) == JsonConvert.SerializeObject(b, Settings);
}
