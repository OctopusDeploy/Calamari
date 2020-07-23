using System;

namespace Calamari.Common.Features.Deployment
{
    public static class DeploymentStages
    {
        public const string BeforePreDeploy = "BeforePreDeploy";
        public const string PreDeploy = "PreDeploy";
        public const string AfterPreDeploy = "AfterPreDeploy";

        public const string BeforeDeploy = "BeforeDeploy";
        public const string Deploy = "Deploy";
        public const string AfterDeploy = "AfterDeploy";

        public const string BeforePostDeploy = "BeforePostDeploy";
        public const string PostDeploy = "PostDeploy";
        public const string AfterPostDeploy = "AfterPostDeploy";

        public const string DeployFailed = "DeployFailed";
    }
}