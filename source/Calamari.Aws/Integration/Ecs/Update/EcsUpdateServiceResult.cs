using Amazon.ECS.Model;

namespace Calamari.Aws.Integration.Ecs.Update;

public record EcsUpdateServiceResult(
    TaskDefinition OriginalTaskDefinition,
    TaskDefinition NewTaskDefinition, // null when no new revision was registered
    Service UpdatedService);
