using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Deployment.Integration.ResourceGroups;
using Calamari.Azure.Integration;
using Calamari.Shared.Commands;

namespace Calamari.Azure.Commands
{
    public interface IModule
    {
        void Registration(IContiner continer);
    }

    public interface IContiner
    {
        void Register<T>();
        void RegisterAs<T, Z>() where T : Z;
        void RegisterAs<T, Z>(Z instance) where T : Z;
    }

    public class AzureModule : IModule
    {
        public void Registration(IContiner continer)
        {
            continer.RegisterAs<SubscriptionCloudCredentialsFactory, ISubscriptionCloudCredentialsFactory>();
            continer.RegisterAs<AzureCloudServiceConfigurationRetriever, IAzureCloudServiceConfigurationRetriever>();
            continer.RegisterAs<ResourceGroupTemplateNormalizer, IResourceGroupTemplateNormalizer>();
            continer.Register<AzurePackageUploader>();
        }
    }
    
    
    [DeploymentAction("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceCommand : IDeploymentAction
    {
        public void Build(IDeploymentStrategyBuilder builder)
        {
            builder
                .AddConvention<SwapAzureDeploymentConvention>()
                .AddExtractPackageToStagingDirectory()
                .AddConvention<FindCloudServicePackageConvention>()
                .AddConvention<EnsureCloudServicePackageIsCtpFormatConvention>()
                .AddConvention<ExtractAzureCloudServicePackageConvention>()
                .AddConvention<ChooseCloudServiceConfigurationFileConvention>()
                .RunPreScripts()
                .AddConvention<ConfigureAzureCloudServiceConvention>()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .RunDeployScripts()
                .AddConvention<RePackageCloudServiceConvention>()
                .AddConvention<UploadAzureCloudServicePackageConvention>()
                .AddConvention<DeployAzureCloudServicePackageConvention>()
                .RunPostScripts();
        }
    }
}
