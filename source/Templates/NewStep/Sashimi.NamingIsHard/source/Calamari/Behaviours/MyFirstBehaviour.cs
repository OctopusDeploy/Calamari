using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.NamingIsHard.Behaviours
{
    class MyFirstBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public MyFirstBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            log.Info("Hello from MyFirstBehaviour");

            return this.CompletedTask();
        }
    }
}