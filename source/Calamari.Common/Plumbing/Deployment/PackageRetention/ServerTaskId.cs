using System;
using System.ComponentModel;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    //[TypeConverter(typeof(TinyTypeTypeConverter<ServerTaskID>))]
    public class ServerTaskId : CaseInsensitiveTinyType
    {
        public ServerTaskId(string value) : base(value)
        {
        }

        public ServerTaskId(IVariables variables)
            : base(variables.Get(KnownVariables.ServerTask.Id)
                   ?? throw new Exception("ServerTask.Id not set."))
        {
        }
    }
}