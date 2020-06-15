using Autofac;
using Calamari.Aws.Deployment.CloudFormation;
using Calamari.Aws.Integration.S3;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;

namespace Calamari.Aws
{
    public class Program : CalamariFlavourProgram
    {
        public Program(ILog log) : base(log)
        {
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            builder.Register(c => AwsEnvironmentGeneration.Create(c.Resolve<ILog>(), c.Resolve<IVariables>()))
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<AmazonClientFactory>()
                .As<IAmazonClientFactory>()
                .SingleInstance();

            builder.RegisterType<CloudFormationService>()
                .As<ICloudFormationService>();

            builder.RegisterType<VariableS3TargetOptionsProvider>()
                .As<IProvideS3TargetOptions>()
                .SingleInstance();

            base.ConfigureContainer(builder, options);
        }

        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}