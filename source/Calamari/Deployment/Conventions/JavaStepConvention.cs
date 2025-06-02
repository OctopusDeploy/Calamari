using System;
using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Features.Java;
using Calamari.Deployment.Features.Java.Actions;

namespace Calamari.Deployment.Conventions
{
    public class JavaStepConvention : IInstallConvention
    {
        readonly string actionType;
        readonly JavaRunner javaRunner;
        readonly ILog log;

        public JavaStepConvention(string actionType, JavaRunner javaRunner, ILog log)
        {
            this.actionType = actionType;
            this.javaRunner = javaRunner;
            this.log = log;
        }

        readonly Dictionary<string, Type> javaStepTypes = new Dictionary<string, Type>()
        {
            {SpecialVariables.Action.Java.JavaKeystore.CertificateActionTypeName, typeof(JavaKeystoreAction)},
            {SpecialVariables.Action.Java.WildFly.CertificateActionTypeName, typeof(WildflyDeployCertificateAction)},
            {SpecialVariables.Action.Java.Tomcat.StateActionTypeName, typeof(TomcatStateAction)},
            {SpecialVariables.Action.Java.WildFly.StateActionTypeName, typeof(WildFlyStateAction)},
            {SpecialVariables.Action.Java.TomcatDeployCertificate.CertificateActionTypeName, typeof(TomcatDeployCertificateAction)}
        };

        public void Install(RunningDeployment deployment)
        {
            if (javaStepTypes.TryGetValue(actionType, out var stepType))
            {
                var javaStep = (JavaAction)Activator.CreateInstance(stepType, new object[] {javaRunner, log});
                javaStep.Execute(deployment);
            }
            else
            {
                throw new CommandException($"Unknown step type `{actionType}`");
            }
        }
    }
}