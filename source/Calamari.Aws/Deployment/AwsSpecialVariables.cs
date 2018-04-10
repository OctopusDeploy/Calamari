using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Aws.Deployment
{
    public static class AwsSpecialVariables
    {
        public static class S3
        {
            public const string FileSelections = "Octopus.Action.Aws.S3.FileSelections";
            public const string PackageOptions = "Octopus.Action.Aws.S3.PackageOptions";
        }
    }
}
