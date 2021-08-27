using System;
using System.ComponentModel;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    //[TypeConverter(typeof(TinyTypeTypeConverter<ServerTaskID>))]
    public class ServerTaskID : CaseInsensitiveTinyType
    {
        public ServerTaskID(string value) : base(value)
        {
        }

        public ServerTaskID(IVariables variables) : base(variables.Get(KnownVariables.ServerTask.Id))
        {
            //TODO: do we need validation on the task id here?
        }
    }
}