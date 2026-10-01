using Calamari.Common.Plumbing.Variables;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Inputs.Ecs;

public interface IEcsImageNameResolver
{
    string ResolveImageName(ContainerImageReference imageReference, IVariables variables);
}

public class EcsImageNameResolver : IEcsImageNameResolver
{
    public string ResolveImageName(ContainerImageReference imageReference, IVariables variables) => variables.Get(PackageVariables.IndexedImage(imageReference.ReferenceId));
}
