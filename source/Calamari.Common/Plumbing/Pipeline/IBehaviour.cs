using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;

namespace Calamari.Common.Plumbing.Pipeline
{
    public interface IBehaviour
    {
        public bool IsEnabled(RunningDeployment context);
        public Task Execute(RunningDeployment context);
    }
}