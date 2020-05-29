using System;

namespace Calamari.Aws.Deployment.CloudFormation
{
    public static class CloudFormationDefaults
    {
        public static readonly TimeSpan StatusWaitPeriod;
        public static readonly int RetryCount = 3;

        static CloudFormationDefaults()
        {
            StatusWaitPeriod = TimeSpan.FromSeconds(5);
        }
    }
}