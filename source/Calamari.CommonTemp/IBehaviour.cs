using System.Threading.Tasks;
using Calamari.Deployment;

namespace Calamari.CommonTemp
{
    public interface IBehaviour
    {
        public Task Execute(RunningDeployment context);
    }
}