using System;
using System.ComponentModel;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class ServerTaskId : CaseInsensitiveTinyType
    {
        [JsonConstructor]
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