using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Aws.Commands;

namespace Calamari.Aws
{
    public class AwsExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"apply-aws-cloudformation-changeset", typeof(ApplyCloudFormationChangesetCommand)},
            {"delete-aws-cloudformation", typeof(DeleteCloudFormationCommand)},
            {"deploy-aws-cloudformation", typeof(DeployCloudFormationCommand)},
            {"upload-aws-s3", typeof(UploadAwsS3Command)},
        };
        
        public Dictionary<string, Type> RegisterCommands()
        {
            return commandTypes;
        }

        public void Load(ContainerBuilder builder)
        {
        }
    }
}