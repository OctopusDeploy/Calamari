using System;
using System.Threading.Tasks;
using Calamari.Deployment.Conventions;

namespace Calamari.Kubernetes
{
    /// <summary>
    /// This wrapper exists because we aren't able to directly inject the <see cref="IAwsAuthConventionFactory"/> anywhere
    /// because for .NET Framework 4.0 versions of Calamari, an implementation won't be registered. We can't even inject
    /// <see cref="Lazy{IAwsAuthConventionFactory}"/> for the same reason. However if we wrap that type in this wrapper
    /// and inject this as a <see cref="Lazy{AwsAuthConventionFactoryWrapper}"/>, Autofac allows it and then we can use
    /// it only when we know we need it so it won't cause issues on .NET 4.0 versions of Calamari.
    /// This can be removed once we remove the dependency on .NET 4.0.
    /// </summary>
    public class AwsAuthConventionFactoryWrapper
    {
        private readonly IAwsAuthConventionFactory awsEnvironmentVariablesGenerator;

        public AwsAuthConventionFactoryWrapper(IAwsAuthConventionFactory awsEnvironmentVariablesGenerator)
        {
            this.awsEnvironmentVariablesGenerator = awsEnvironmentVariablesGenerator;
        }

        public IInstallConvention Create(Func<Task<bool>> verifyLogin = null)
        {
            return awsEnvironmentVariablesGenerator.Create(verifyLogin);
        }
    }
}