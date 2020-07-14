using System.Threading.Tasks;
using Calamari.Deployment;

namespace Calamari.CommonTemp
{
    public interface IBehaviour
    {
        public bool IsEnabled(RunningDeployment context);
        public Task Execute(RunningDeployment context);
    }
}