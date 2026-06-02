using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Aws.Behaviours;

public class EcsClusterDiscoveryBehaviour : IDeployBehaviour
{
    public bool IsEnabled(RunningDeployment context) => true;
    

    public Task Execute(RunningDeployment context)
    {
        return Task.CompletedTask;
    }
}