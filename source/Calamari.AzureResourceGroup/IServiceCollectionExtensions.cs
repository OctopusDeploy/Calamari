using Bicep.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Calamari.AzureResourceGroup
{
    public static class ServiceCollectionExtensions
    {
        // TODO: this method should be available in the next release of Bicep.Core
        // at the moment, there is a `AddBicepCore` method that registers the required dependencies
        // that is available on master. It hasn't been released to NuGet however :)
        public static IServiceCollection AddBicep(this IServiceCollection services) 
            => services.AddBicepCore().AddSingleton<IBicepTemplateCompiler, BicepTemplateCompiler>();
    }
}