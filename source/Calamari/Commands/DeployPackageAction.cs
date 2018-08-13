using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.Iis;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageAction : IDeploymentAction
    {
        private readonly ICalamariFileSystem filesystem;

        public DeployPackageAction(ICalamariFileSystem filesystem)
        {
            this.filesystem = filesystem;
        }
        
        public void Build(IDeploymentStrategyBuilder cb)
        {
            cb.UsesDeploymentJournal = true;

#if IIS_SUPPORT
            cb.Features.Add<IisWebSiteBeforeDeployFeature>();
            cb.Features.Add<IisWebSiteAfterPostDeployFeature>();
            var iis = new InternetInformationServer();
#endif
            
            cb.AddExtractPackageToApplicationDirectory()
                .RunPreScripts()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .AddConvention<CopyPackageToCustomInstallationDirectoryConvention>()
                .RunDeployScripts();
            
#if IIS_SUPPORT
            cb.AddConvention(new LegacyIisWebSiteConvention(filesystem, iis));
#endif

            cb.RunPostScripts();
        }
    }
}
