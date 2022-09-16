using Calamari.Common.Commands;

namespace Calamari.Deployment.Features.Java.Actions
{
    public abstract class JavaAction
    {
        protected readonly JavaRunner runner;
        public JavaAction(JavaRunner runner)
        {
            this.runner = runner;
        }

        public abstract void Execute(RunningDeployment deployment);
    }
}