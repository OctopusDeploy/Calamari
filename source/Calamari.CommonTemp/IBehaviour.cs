using System.Threading.Tasks;
using Calamari.Common.Commands;

namespace Calamari.CommonTemp
{
    public interface IBehaviour
    {
        public bool IsEnabled(RunningDeployment context);
        public Task Execute(RunningDeployment context);
    }
}